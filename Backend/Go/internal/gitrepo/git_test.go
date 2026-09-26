package gitrepo

import (
	"context"
	"errors"
	"os"
	"path/filepath"
	"runtime"
	"testing"
	"time"

	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/testutil"
)

func TestCommandTimeoutBoundsGit(t *testing.T) {
	if runtime.GOOS == "windows" {
		t.Skip("fake git is a POSIX shell script")
	}

	dir := t.TempDir()
	fakeGit := filepath.Join(dir, "git")
	if err := os.WriteFile(fakeGit, []byte("#!/bin/sh\nsleep 30\n"), 0o755); err != nil {
		t.Fatal(err)
	}

	g := Git{Binary: fakeGit, Timeout: 150 * time.Millisecond}
	started := time.Now()
	_, err := g.ListFiles(context.Background(), dir)
	if !errors.Is(err, context.DeadlineExceeded) {
		t.Fatalf("err = %v, want context.DeadlineExceeded", err)
	}
	if elapsed := time.Since(started); elapsed > 10*time.Second {
		t.Fatalf("elapsed = %v, per-command timeout not enforced", elapsed)
	}
}

func TestMarkerStatsCountsWholeWords(t *testing.T) {
	repo := testutil.NewRepo(t,
		testutil.Commit{Author: "Alice", Email: "alice@example.com", Date: "2026-01-01T10:00:00Z", File: "a.go",
			Body: "package a\n// TODO: one\n// TODO: two\n// FIXME: three\nvar TODO_LIST = 1\n"},
		testutil.Commit{Author: "Alice", Email: "alice@example.com", Date: "2026-02-01T10:00:00Z", File: "sub/b.go",
			Body: "// TODO: four\n"},
	)

	var g Git
	stats, err := g.MarkerStats(context.Background(), repo)
	if err != nil {
		t.Fatalf("MarkerStats: %v", err)
	}
	if stats.TodoCount != 3 {
		t.Errorf("TodoCount = %d, want 3", stats.TodoCount)
	}
	if stats.FixmeCount != 1 {
		t.Errorf("FixmeCount = %d, want 1", stats.FixmeCount)
	}
	if !stats.HasOldest {
		t.Fatal("HasOldest = false")
	}
	if got := stats.OldestAt.UTC().Format(time.DateOnly); got != "2026-01-01" {
		t.Errorf("OldestAt = %s, want 2026-01-01", got)
	}
	if age := time.Since(stats.OldestAt); age <= 0 {
		t.Errorf("age = %v, want > 0", age)
	}
}

func TestMarkerStatsEmptyRepo(t *testing.T) {
	repo := testutil.NewRepo(t)

	var g Git
	stats, err := g.MarkerStats(context.Background(), repo)
	if err != nil {
		t.Fatalf("MarkerStats: %v", err)
	}
	if stats.TodoCount != 0 || stats.FixmeCount != 0 || stats.HasOldest {
		t.Errorf("stats = %+v, want zero", stats)
	}
}
