package service

import (
	"context"
	"encoding/json"
	"io"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"testing"
	"time"

	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/appsec"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/contract"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/gitrepo"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/objectstore"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/snapshot"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/sourcecraft"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/testutil"
)

func newDocumentationService(t *testing.T, repoPath string) *Service {
	t.Helper()
	fake := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.URL.Path != "/repos/id:r1" {
			w.WriteHeader(http.StatusNotFound)
			return
		}
		_ = json.NewEncoder(w).Encode(map[string]any{
			"id": "r1", "slug": "demo", "default_branch": "main", "visibility": "public",
			"organization": map[string]string{"slug": "acme"},
			"clone_url":    map[string]string{"https": repoPath},
		})
	}))
	t.Cleanup(fake.Close)

	log := slog.New(slog.NewTextHandler(io.Discard, nil))
	objects := objectstore.NewMemory()
	git := gitrepo.Git{}
	store := snapshot.NewStore(objects, git, snapshot.Options{Prefix: "snapshots", TTL: time.Hour, WorkDir: t.TempDir()}, log)
	client := sourcecraft.NewClient(sourcecraft.Options{BaseURL: fake.URL, Timeout: 5 * time.Second, MaxRetries: 1})
	return New(client, appsec.NewClient(appsec.Options{}), store, git, "", t.TempDir(), Limits{MaxResponseLookups: 10})
}

func TestDocumentationDetectsInstructions(t *testing.T) {
	repo := testutil.NewRepo(t,
		testutil.Commit{Author: "Alice", Email: "alice@example.com", Date: "2026-01-01T10:00:00Z", File: "README.md",
			Body: "# Demo\nA small utility for demo purposes.\n"},
		testutil.Commit{Author: "Alice", Email: "alice@example.com", Date: "2026-01-02T10:00:00Z", File: "CONTRIBUTING.md",
			Body: "Please run the test suite and build the project with make.\n"},
	)
	svc := newDocumentationService(t, repo)

	report, err := svc.Documentation(context.Background(), "", "r1", "")
	if err != nil {
		t.Fatalf("Documentation: %v", err)
	}
	if !report.HasReadme {
		t.Error("HasReadme = false")
	}
	if !report.HasContributing {
		t.Error("HasContributing = false")
	}
	if !report.HasLocalRunInstructions {
		t.Error("HasLocalRunInstructions = false, want true from CONTRIBUTING")
	}
	if !report.HasBuildAndTestInstructions {
		t.Error("HasBuildAndTestInstructions = false, want true from CONTRIBUTING")
	}
}

func TestSecurityFindingsMapsAppSec(t *testing.T) {
	fakeSC := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.URL.Path != "/repos/id:r1" {
			w.WriteHeader(http.StatusNotFound)
			return
		}
		_ = json.NewEncoder(w).Encode(map[string]any{"id": "internal-42", "slug": "demo"})
	}))
	t.Cleanup(fakeSC.Close)

	var gotGitRepo string
	fakeAppSec := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.URL.Path != "/v1/defect-groups" {
			w.WriteHeader(http.StatusNotFound)
			return
		}
		gotGitRepo = r.URL.Query().Get("gitRepo")
		_, _ = w.Write([]byte(`{"data":[
			{"uuid":"f1","ruleId":"CWE-79","ruleName":"XSS","fileName":"a.go","engineType":2,"severity":4,"status":0},
			{"uuid":"f2","ruleId":"SECRET","engine":"SECRETS","engineType":99,"severity":0,"status":1},
			{"uuid":"f3","ruleId":"DEP","engineType":1,"severity":2,"status":2}
		]}`))
	}))
	t.Cleanup(fakeAppSec.Close)

	log := slog.New(slog.NewTextHandler(io.Discard, nil))
	objects := objectstore.NewMemory()
	git := gitrepo.Git{}
	store := snapshot.NewStore(objects, git, snapshot.Options{Prefix: "snapshots", TTL: time.Hour, WorkDir: t.TempDir()}, log)
	api := sourcecraft.NewClient(sourcecraft.Options{BaseURL: fakeSC.URL, Timeout: 5 * time.Second, MaxRetries: 1})
	appSec := appsec.NewClient(appsec.Options{BaseURL: fakeAppSec.URL, Timeout: 5 * time.Second, MaxRetries: 1})
	svc := New(api, appSec, store, git, "", t.TempDir(), Limits{})

	findings, err := svc.SecurityFindings(context.Background(), "pat", "r1")
	if err != nil {
		t.Fatal(err)
	}
	if gotGitRepo != "internal-42" {
		t.Errorf("gitRepo = %q, want internal id", gotGitRepo)
	}
	if len(findings) != 3 {
		t.Fatalf("findings = %+v", findings)
	}
	if findings[0].Kind != contract.SecurityFindingSast || findings[0].Severity != contract.SecuritySeverityCritical || findings[0].Status != contract.SecurityFindingOpen {
		t.Errorf("f1 = %+v", findings[0])
	}
	if findings[1].Kind != contract.SecurityFindingSecretScanning || findings[1].Severity != contract.SecuritySeverityLow || findings[1].Status != contract.SecurityFindingFixed {
		t.Errorf("f2 = %+v", findings[1])
	}
	if findings[2].Kind != contract.SecurityFindingSca || findings[2].Severity != contract.SecuritySeverityMedium {
		t.Errorf("f3 = %+v", findings[2])
	}
}

func TestDocumentationIgnoresSubstrings(t *testing.T) {
	repo := testutil.NewRepo(t,
		testutil.Commit{Author: "Alice", Email: "alice@example.com", Date: "2026-01-01T10:00:00Z", File: "README.md",
			Body: "# Demo\nThe runtime uses the latest contest version.\n"},
	)
	svc := newDocumentationService(t, repo)

	report, err := svc.Documentation(context.Background(), "", "r1", "")
	if err != nil {
		t.Fatalf("Documentation: %v", err)
	}
	if !report.HasReadme {
		t.Error("HasReadme = false")
	}
	if report.HasLocalRunInstructions {
		t.Error("HasLocalRunInstructions = true, want false: runtime must not match run")
	}
	if report.HasBuildAndTestInstructions {
		t.Error("HasBuildAndTestInstructions = true, want false: latest/contest must not match test")
	}
}
