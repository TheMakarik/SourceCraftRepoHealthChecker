package gitrepo

import (
	"context"
	"testing"
	"time"

	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/testutil"
)

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
