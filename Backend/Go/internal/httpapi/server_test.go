package httpapi

import (
	"encoding/json"
	"io"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
	"time"

	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/gitrepo"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/objectstore"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/service"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/snapshot"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/sourcecraft"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/testutil"
)

type env struct {
	api     *httptest.Server
	objects *objectstore.Memory
}

// newEnv поднимает фейковый SourceCraft (repoPath — clone_url репозитория r1) и наш API поверх него.
func newEnv(t *testing.T, repoPath string, extra http.HandlerFunc) *env {
	t.Helper()
	fake := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.Header.Get("Authorization") != "Bearer user-pat" {
			w.WriteHeader(http.StatusUnauthorized)
			_, _ = w.Write([]byte(`{"error_code":"unauthorized","message":"Unauthorized"}`))
			return
		}
		switch r.URL.Path {
		case "/user":
			_, _ = w.Write([]byte(`{"id":"u1","username":"alice","display_name":"Alice"}`))
		case "/repos/id:r1":
			_ = json.NewEncoder(w).Encode(map[string]any{
				"id": "r1", "slug": "demo", "default_branch": "main", "visibility": "public",
				"organization": map[string]string{"slug": "acme"},
				"clone_url":    map[string]string{"https": repoPath},
				"rating":       map[string]any{"reaction_counts": []map[string]string{{"type": "positive_low", "count": "3"}, {"type": "positive_high", "count": "2"}}},
				"language":     map[string]string{"name": "Go"},
			})
		default:
			if extra != nil {
				extra(w, r)
				return
			}
			w.WriteHeader(http.StatusNotFound)
		}
	}))
	t.Cleanup(fake.Close)

	log := slog.New(slog.NewTextHandler(io.Discard, nil))
	objects := objectstore.NewMemory()
	git := gitrepo.Git{}
	store := snapshot.NewStore(objects, git, snapshot.Options{Prefix: "snapshots", TTL: time.Hour, WorkDir: t.TempDir()}, log)
	client := sourcecraft.NewClient(sourcecraft.Options{BaseURL: fake.URL, Timeout: 5 * time.Second, MaxRetries: 1})
	svc := service.New(client, store, git, "", t.TempDir(), service.Limits{MaxResponseLookups: 10})

	api := httptest.NewServer(New(svc, "", "internal-secret", log))
	t.Cleanup(api.Close)
	return &env{api: api, objects: objects}
}

func (e *env) do(t *testing.T, method, path, token string) (int, map[string]any) {
	t.Helper()
	req, _ := http.NewRequest(method, e.api.URL+path, nil)
	if token != "" {
		req.Header.Set("Authorization", "Bearer "+token)
	}
	resp, err := http.DefaultClient.Do(req)
	if err != nil {
		t.Fatal(err)
	}
	defer resp.Body.Close()
	var body map[string]any
	_ = json.NewDecoder(resp.Body).Decode(&body)
	return resp.StatusCode, body
}

func TestSnapshotLifecycleAndActivity(t *testing.T) {
	repo := testutil.NewRepo(t,
		testutil.Commit{Author: "Alice", Email: "alice@example.com", Date: "2026-03-01T10:00:00Z", File: "a.txt", Body: "1"},
		testutil.Commit{Author: "dependabot[bot]", Email: "bot@deps.example", Date: "2026-03-01T15:00:00Z", File: "b.txt", Body: "2"},
		testutil.Commit{Author: "Alice", Email: "ALICE@example.com", Date: "2026-03-03T09:00:00Z", File: "a.txt", Body: "3"},
	)
	e := newEnv(t, repo, nil)

	status, body := e.do(t, http.MethodPut, "/repositories/r1/snapshots/run-42", "user-pat")
	if status != http.StatusCreated {
		t.Fatalf("create: %d %v", status, body)
	}
	if status, _ := e.do(t, http.MethodPut, "/repositories/r1/snapshots/run-42", "user-pat"); status != http.StatusOK {
		t.Fatalf("idempotent create: %d", status)
	}
	if keys := e.objects.Keys(); len(keys) != 1 {
		t.Fatalf("objects = %v", keys)
	}

	status, body = e.do(t, http.MethodGet, "/repositories/r1/activity/commits?runId=run-42", "user-pat")
	data, _ := body["data"].(map[string]any)
	if status != http.StatusOK || body["status"] != "Available" || data["totalCount"] != float64(3) {
		t.Fatalf("commits: %d %v", status, body)
	}
	byDay, _ := data["commitsByDay"].(map[string]any)
	if byDay["2026-03-01"] != float64(2) || byDay["2026-03-03"] != float64(1) {
		t.Fatalf("commitsByDay = %v", byDay)
	}
	if body["requestId"] == "" || body["collectedAt"] == nil {
		t.Fatalf("envelope missing requestId/collectedAt: %v", body)
	}

	status, body = e.do(t, http.MethodGet, "/repositories/r1/activity/contributors?runId=run-42", "user-pat")
	contributors, _ := body["data"].([]any)
	if status != http.StatusOK || len(contributors) != 2 {
		t.Fatalf("contributors: %d %v", status, body)
	}
	for _, c := range contributors {
		c := c.(map[string]any)
		switch c["login"] {
		case "Alice":
			if c["commitsCount"] != float64(2) || c["isBot"] != false {
				t.Errorf("alice = %v", c)
			}
		case "dependabot[bot]":
			if c["isBot"] != true {
				t.Errorf("bot = %v", c)
			}
		default:
			t.Errorf("unexpected contributor %v", c)
		}
	}

	if status, _ := e.do(t, http.MethodDelete, "/repositories/r1/snapshots/run-42", "user-pat"); status != http.StatusNoContent {
		t.Fatalf("delete: %d", status)
	}
	if keys := e.objects.Keys(); len(keys) != 0 {
		t.Fatalf("objects after delete = %v", keys)
	}
	if status, body := e.do(t, http.MethodGet, "/repositories/r1/activity/commits?runId=run-42", "user-pat"); status != http.StatusNotFound {
		t.Fatalf("commits after delete: %d %v", status, body)
	}
}

func TestEphemeralCloneWithoutRunID(t *testing.T) {
	repo := testutil.NewRepo(t, testutil.Commit{Author: "A", Email: "a@x", Date: "2026-01-01T00:00:00Z", File: "f", Body: "x"})
	e := newEnv(t, repo, nil)

	status, body := e.do(t, http.MethodGet, "/repositories/r1/activity/commits", "user-pat")
	if status != http.StatusOK || body["data"].(map[string]any)["totalCount"] != float64(1) {
		t.Fatalf("%d %v", status, body)
	}
	if keys := e.objects.Keys(); len(keys) != 0 {
		t.Fatalf("ephemeral clone must not touch storage: %v", keys)
	}
}

func TestRepositoryMapping(t *testing.T) {
	e := newEnv(t, "unused", nil)
	status, body := e.do(t, http.MethodGet, "/repositories/r1", "user-pat")
	data, _ := body["data"].(map[string]any)
	if status != http.StatusOK || data["fullName"] != "acme/demo" || data["likesCount"] != float64(5) || data["isPrivate"] != false {
		t.Fatalf("%d %v", status, body)
	}
}

func TestErrorMapping(t *testing.T) {
	e := newEnv(t, "unused", func(w http.ResponseWriter, r *http.Request) {
		switch {
		case strings.HasSuffix(r.URL.Path, "/issues"):
			w.WriteHeader(http.StatusServiceUnavailable)
		case strings.HasSuffix(r.URL.Path, "/releases"):
			_, _ = w.Write([]byte(`{"releases":[]}`))
		default:
			w.WriteHeader(http.StatusNotFound)
		}
	})

	cases := []struct {
		name, method, path, token string
		wantCode                  int
		wantStatus                string
	}{
		{"unknown repository", http.MethodGet, "/repositories/nope", "user-pat", http.StatusNotFound, ""},
		{"bad token", http.MethodGet, "/repositories/r1", "wrong", http.StatusUnauthorized, ""},
		{"auth needs user token", http.MethodGet, "/auth/me", "", http.StatusUnauthorized, ""},
		{"source down is Unavailable, not 5xx", http.MethodGet, "/repositories/r1/issues", "user-pat", http.StatusOK, "Unavailable"},
		{"empty list is NoData", http.MethodGet, "/repositories/r1/activity/releases", "user-pat", http.StatusOK, "NoData"},
		{"security is Unavailable", http.MethodGet, "/repositories/r1/security/findings", "user-pat", http.StatusOK, "Unavailable"},
		{"invalid run id", http.MethodPut, "/repositories/r1/snapshots/-bad", "user-pat", http.StatusBadRequest, ""},
		{"reap requires internal token", http.MethodPost, "/internal/snapshots/reap", "", http.StatusForbidden, ""},
	}
	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			code, body := e.do(t, tc.method, tc.path, tc.token)
			if code != tc.wantCode {
				t.Fatalf("code = %d, want %d (%v)", code, tc.wantCode, body)
			}
			if tc.wantStatus != "" && body["status"] != tc.wantStatus {
				t.Fatalf("status = %v, want %s", body["status"], tc.wantStatus)
			}
			if body["requestId"] == "" {
				t.Fatalf("missing requestId: %v", body)
			}
		})
	}
}
