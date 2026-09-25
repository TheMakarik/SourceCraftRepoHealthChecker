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
	return New(client, store, git, "", t.TempDir(), Limits{MaxResponseLookups: 10})
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
