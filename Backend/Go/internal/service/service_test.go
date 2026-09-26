package service

import (
	"context"
	"encoding/json"
	"errors"
	"io"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"runtime"
	"sync"
	"sync/atomic"
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

func TestForEachLimitedRunsAllWithinConcurrency(t *testing.T) {
	service := &Service{limits: Limits{Concurrency: 3}}
	const total = 200

	var (
		mutex      sync.Mutex
		running    int
		maxRunning int
		completed  int
	)
	err := service.forEachLimited(context.Background(), total, func(context.Context, int) error {
		mutex.Lock()
		running++
		if running > maxRunning {
			maxRunning = running
		}
		mutex.Unlock()

		time.Sleep(time.Millisecond)

		mutex.Lock()
		running--
		completed++
		mutex.Unlock()
		return nil
	})
	if err != nil {
		t.Fatalf("forEachLimited: %v", err)
	}
	if completed != total {
		t.Fatalf("completed = %d, want %d", completed, total)
	}
	if maxRunning > 3 {
		t.Fatalf("maxRunning = %d, want <= 3", maxRunning)
	}
}

func TestForEachLimitedPropagatesFirstError(t *testing.T) {
	service := &Service{limits: Limits{Concurrency: 4}}
	want := errors.New("boom")

	err := service.forEachLimited(context.Background(), 100, func(_ context.Context, i int) error {
		if i == 7 {
			return want
		}
		return nil
	})
	if !errors.Is(err, want) {
		t.Fatalf("err = %v, want %v", err, want)
	}
}

func TestForEachLimitedReturnsContextErrorOnCancellation(t *testing.T) {
	service := &Service{limits: Limits{Concurrency: 2}}
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	var started atomic.Int32
	err := service.forEachLimited(ctx, 1_000_000, func(ctx context.Context, _ int) error {
		if started.Add(1) == 1 {
			cancel()
		}
		<-ctx.Done()
		return ctx.Err()
	})
	if !errors.Is(err, context.Canceled) {
		t.Fatalf("err = %v, want context.Canceled", err)
	}
	if started.Load() >= 1000 {
		t.Fatalf("started = %d, cancellation was not respected", started.Load())
	}
}

func TestForEachLimitedReturnsContextErrorOnDeadline(t *testing.T) {
	service := &Service{limits: Limits{Concurrency: 2}}
	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Millisecond)
	defer cancel()

	err := service.forEachLimited(ctx, 1_000_000, func(ctx context.Context, _ int) error {
		<-ctx.Done()
		return ctx.Err()
	})
	if !errors.Is(err, context.DeadlineExceeded) {
		t.Fatalf("err = %v, want context.DeadlineExceeded", err)
	}
}

func TestForEachLimitedDoesNotStartAfterCancel(t *testing.T) {
	service := &Service{limits: Limits{Concurrency: 4}}
	ctx, cancel := context.WithCancel(context.Background())
	cancel()

	var calls atomic.Int32
	started := time.Now()
	err := service.forEachLimited(ctx, 1_000_000, func(context.Context, int) error {
		calls.Add(1)
		return nil
	})
	if !errors.Is(err, context.Canceled) {
		t.Fatalf("err = %v, want context.Canceled", err)
	}
	if calls.Load() != 0 {
		t.Fatalf("fn calls = %d, want 0 for an already cancelled context", calls.Load())
	}
	if elapsed := time.Since(started); elapsed > time.Second {
		t.Fatalf("elapsed = %v, want prompt return", elapsed)
	}
}

func TestForEachLimitedDoesNotLeakGoroutines(t *testing.T) {
	service := &Service{limits: Limits{Concurrency: 4}}
	_ = service.forEachLimited(context.Background(), 8, func(context.Context, int) error { return nil })
	baseline := runtime.NumGoroutine()

	for range 50 {
		if err := service.forEachLimited(context.Background(), 200, func(context.Context, int) error { return nil }); err != nil {
			t.Fatalf("forEachLimited: %v", err)
		}
	}

	const slack = 5
	deadline := time.Now().Add(2 * time.Second)
	for runtime.NumGoroutine() > baseline+slack && time.Now().Before(deadline) {
		time.Sleep(10 * time.Millisecond)
	}
	if n := runtime.NumGoroutine(); n > baseline+slack {
		t.Fatalf("goroutines = %d, baseline = %d: goroutine leak", n, baseline)
	}
}

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
