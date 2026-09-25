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
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/yandexid"
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

	api := httptest.NewServer(New(svc, Options{InternalToken: "internal-secret"}, log))
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
		{"yandex id not configured", http.MethodPost, "/auth/url", "", http.StatusNotImplemented, ""},
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

func TestYandexIDLogin(t *testing.T) {
	fake := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		switch r.URL.Path {
		case "/token":
			_ = r.ParseForm()
			if r.PostForm.Get("code") != "good-code" {
				w.WriteHeader(http.StatusBadRequest)
				_, _ = w.Write([]byte(`{"error":"invalid_grant"}`))
				return
			}
			_, _ = w.Write([]byte(`{"access_token":"ya-token"}`))
		case "/info":
			_, _ = w.Write([]byte(`{"id":"42","login":"ivan","display_name":"Ivan"}`))
		}
	}))
	t.Cleanup(fake.Close)

	log := slog.New(slog.NewTextHandler(io.Discard, nil))
	yid := yandexid.New(yandexid.Options{ClientID: "app-id", ClientSecret: "app-secret", OAuthURL: fake.URL, LoginURL: fake.URL})
	api := httptest.NewServer(New(nil, Options{YandexID: yid}, log))
	t.Cleanup(api.Close)

	post := func(path, body string) (int, map[string]any, string) {
		t.Helper()
		resp, err := http.Post(api.URL+path, "application/json", strings.NewReader(body))
		if err != nil {
			t.Fatal(err)
		}
		defer resp.Body.Close()
		raw, _ := io.ReadAll(resp.Body)
		var m map[string]any
		_ = json.Unmarshal(raw, &m)
		return resp.StatusCode, m, string(raw)
	}

	code, body, _ := post("/auth/url", `{"state":"xyz"}`)
	data, _ := body["data"].(map[string]any)
	if code != http.StatusOK || !strings.Contains(data["url"].(string), "state=xyz") {
		t.Fatalf("auth url: %d %v", code, body)
	}
	if code, _, _ := post("/auth/url", `{}`); code != http.StatusBadRequest {
		t.Fatalf("auth url without state: %d", code)
	}

	code, body, raw := post("/auth/token", `{"code":"good-code","state":"xyz"}`)
	user, _ := body["data"].(map[string]any)
	if code != http.StatusOK || user["id"] != "42" || user["login"] != "ivan" || user["displayName"] != "Ivan" || user["email"] != nil {
		t.Fatalf("auth token: %d %v", code, body)
	}
	if strings.Contains(raw, "ya-token") {
		t.Fatal("Yandex token leaked into response")
	}

	if code, body, _ := post("/auth/token", `{"code":"stale","state":"xyz"}`); code != http.StatusBadRequest || body["code"] != "invalid_grant" {
		t.Fatalf("stale code: %d %v", code, body)
	}
}

func (e *env) doJSON(t *testing.T, method, path, token, body string) (int, map[string]any) {
	t.Helper()
	req, _ := http.NewRequest(method, e.api.URL+path, strings.NewReader(body))
	req.Header.Set("Authorization", "Bearer "+token)
	req.Header.Set("Content-Type", "application/json")
	resp, err := http.DefaultClient.Do(req)
	if err != nil {
		t.Fatal(err)
	}
	defer resp.Body.Close()
	var out map[string]any
	_ = json.NewDecoder(resp.Body).Decode(&out)
	return resp.StatusCode, out
}

func TestDocumentationAndCodeHealth(t *testing.T) {
	readme := "# Demo\n\n## Quick start\n```bash\ndocker compose up\n```\n\n## Build and test\n```bash\ngo build ./...\ngo test ./...\n```\n"
	repo := testutil.NewRepo(t,
		testutil.Commit{Author: "A", Email: "a@x", Date: "2023-01-01T00:00:00Z", File: "main.go", Body: "package main\n// TODO: old debt\n"},
		testutil.Commit{Author: "A", Email: "a@x", Date: "2026-01-01T00:00:00Z", File: "main.go", Body: "package main\n// TODO: old debt\n// FIXME: new XXX\nvar TODOS = 1\n"},
		testutil.Commit{Author: "A", Email: "a@x", Date: "2026-01-02T00:00:00Z", File: "vendor/dep.go", Body: "// TODO: not ours\n"},
		testutil.Commit{Author: "A", Email: "a@x", Date: "2026-01-03T00:00:00Z", File: "README.md", Body: readme},
		testutil.Commit{Author: "A", Email: "a@x", Date: "2026-01-03T00:00:00Z", File: "LICENSE", Body: "MIT"},
		testutil.Commit{Author: "A", Email: "a@x", Date: "2026-01-03T00:00:00Z", File: ".sourcecraft/CODEOWNERS", Body: "* @a"},
	)
	e := newEnv(t, repo, nil)

	// Снапшот без блобов не годится для анализа содержимого — явный 409, а не тихий ноль.
	if code, body := e.doJSON(t, http.MethodPut, "/repositories/r1/snapshots/meta-only", "user-pat", ""); code != http.StatusCreated {
		t.Fatalf("create meta-only: %d %v", code, body)
	}
	if code, body := e.do(t, http.MethodGet, "/repositories/r1/code-health?runId=meta-only", "user-pat"); code != http.StatusConflict || body["code"] != "snapshot_without_blobs" {
		t.Fatalf("code-health on meta-only snapshot: %d %v", code, body)
	}

	if code, body := e.doJSON(t, http.MethodPut, "/repositories/r1/snapshots/full", "user-pat", `{"withBlobs":true}`); code != http.StatusCreated {
		t.Fatalf("create full: %d %v", code, body)
	}

	code, body := e.do(t, http.MethodGet, "/repositories/r1/code-health?runId=full", "user-pat")
	data, _ := body["data"].(map[string]any)
	if code != http.StatusOK || body["status"] != "Available" ||
		data["todoCount"] != float64(1) || data["fixmeCount"] != float64(1) || data["totalCommentCount"] != float64(3) {
		t.Fatalf("code-health: %d %v", code, body)
	}
	// Самый старый TODO — 2023-01-01, т.е. больше тысячи дней: формат TimeSpan "d.hh:mm:ss".
	age, _ := data["oldestCommentAge"].(string)
	if days, _, ok := strings.Cut(age, "."); !ok || len(days) < 4 {
		t.Fatalf("oldestCommentAge = %q", age)
	}

	want := map[string]any{
		"hasReadme": true, "hasLicense": true, "hasContributing": false, "hasCodeOwners": true,
		"hasLocalRunInstructions": true, "hasBuildAndTestInstructions": true,
	}
	for _, path := range []string{"/repositories/r1/documentation?runId=full", "/repositories/r1/documentation"} {
		code, body := e.do(t, http.MethodGet, path, "user-pat")
		data, _ := body["data"].(map[string]any)
		if code != http.StatusOK {
			t.Fatalf("%s: %d %v", path, code, body)
		}
		for k, v := range want {
			if data[k] != v {
				t.Errorf("%s: %s = %v, want %v", path, k, data[k], v)
			}
		}
	}
}
