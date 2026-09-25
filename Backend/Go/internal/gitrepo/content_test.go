package gitrepo_test

import (
	"context"
	"errors"
	"os/exec"
	"path/filepath"
	"slices"
	"testing"
	"time"

	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/gitrepo"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/testutil"
)

func bareClone(t *testing.T, src string, opts gitrepo.CloneOptions) string {
	t.Helper()
	dir := filepath.Join(t.TempDir(), "repo.git")
	opts.URL = src
	if err := (gitrepo.Git{}).CloneBare(context.Background(), dir, opts); err != nil {
		t.Fatal(err)
	}
	return dir
}

func fixture(t *testing.T) string {
	return testutil.NewRepo(t,
		testutil.Commit{Author: "A", Email: "a@x", Date: "2024-01-01T00:00:00Z", File: "dir: odd/f.go", Body: "x // TODO: first\nclean\n"},
		testutil.Commit{Author: "B", Email: "b@x", Date: "2025-06-01T00:00:00Z", File: "dir: odd/f.go", Body: "x // TODO: first\nclean\n# FIXME later\nTODOS no\n"},
		testutil.Commit{Author: "B", Email: "b@x", Date: "2025-06-02T00:00:00Z", File: "vendor/lib.go", Body: "// TODO vendored\n"},
	)
}

func TestContentOperations(t *testing.T) {
	g := gitrepo.Git{}
	ctx := context.Background()
	dir := bareClone(t, fixture(t), gitrepo.CloneOptions{WithBlobs: true})

	var files []string
	if err := g.WalkFiles(ctx, dir, "HEAD", func(p string) error { files = append(files, p); return nil }); err != nil {
		t.Fatal(err)
	}
	if !slices.Equal(files, []string{"dir: odd/f.go", "vendor/lib.go"}) {
		t.Fatalf("files = %q", files)
	}

	data, err := g.ReadFile(ctx, dir, "HEAD", "dir: odd/f.go", 1<<10)
	if err != nil || string(data) != "x // TODO: first\nclean\n# FIXME later\nTODOS no\n" {
		t.Fatalf("read: %q %v", data, err)
	}
	if _, err := g.ReadFile(ctx, dir, "HEAD", "dir: odd/f.go", 5); !errors.Is(err, gitrepo.ErrFileTooLarge) {
		t.Fatalf("limit: %v", err)
	}

	var matches []gitrepo.GrepMatch
	err = g.Grep(ctx, dir, "HEAD", `(^|[^A-Za-z0-9_])(TODO|FIXME)([^A-Za-z0-9_]|$)`, []string{"**/vendor/**", "vendor/**"}, func(m gitrepo.GrepMatch) error {
		matches = append(matches, m)
		return nil
	})
	if err != nil {
		t.Fatal(err)
	}
	want := []gitrepo.GrepMatch{{Path: "dir: odd/f.go", Line: 1, Text: "x // TODO: first"}, {Path: "dir: odd/f.go", Line: 3, Text: "# FIXME later"}}
	if !slices.Equal(matches, want) {
		t.Fatalf("grep = %+v", matches)
	}

	// Нет совпадений — не ошибка.
	if err := g.Grep(ctx, dir, "HEAD", "NOPE_NEVER", nil, func(gitrepo.GrepMatch) error { t.Fatal("unexpected match"); return nil }); err != nil {
		t.Fatalf("no matches: %v", err)
	}

	times, err := g.BlameTimes(ctx, dir, "HEAD", "dir: odd/f.go", []int{1, 3})
	if err != nil {
		t.Fatal(err)
	}
	if !times[1].Equal(time.Date(2024, 1, 1, 0, 0, 0, 0, time.UTC)) || !times[3].Equal(time.Date(2025, 6, 1, 0, 0, 0, 0, time.UTC)) {
		t.Fatalf("blame = %v", times)
	}
}

func TestMissingBlobsAreNotFetched(t *testing.T) {
	src := fixture(t)
	if out, err := exec.Command("git", "-C", src, "config", "uploadpack.allowFilter", "true").CombinedOutput(); err != nil {
		t.Fatalf("%v: %s", err, out)
	}
	dir := bareClone(t, "file://"+src, gitrepo.CloneOptions{})

	_, err := (gitrepo.Git{}).ReadFile(context.Background(), dir, "HEAD", "dir: odd/f.go", 1<<10)
	if !errors.Is(err, gitrepo.ErrMissingObjects) {
		t.Fatalf("read: err = %v, want ErrMissingObjects", err)
	}

	// git grep на partial clone выходит с кодом 1, как при «нет совпадений», — это нельзя принять за ноль TODO.
	err = (gitrepo.Git{}).Grep(context.Background(), dir, "HEAD", "TODO", nil, func(gitrepo.GrepMatch) error { return nil })
	if !errors.Is(err, gitrepo.ErrMissingObjects) {
		t.Fatalf("grep: err = %v, want ErrMissingObjects", err)
	}
}

func TestEmptyRepository(t *testing.T) {
	src := testutil.NewRepo(t)
	dir := filepath.Join(t.TempDir(), "repo.git")
	if out, err := exec.Command("git", "clone", "--bare", "--quiet", src, dir).CombinedOutput(); err != nil {
		t.Fatalf("%v: %s", err, out)
	}
	err := (gitrepo.Git{}).WalkFiles(context.Background(), dir, "HEAD", func(string) error { return nil })
	if !errors.Is(err, gitrepo.ErrNoRevision) {
		t.Fatalf("err = %v, want ErrNoRevision", err)
	}
}
