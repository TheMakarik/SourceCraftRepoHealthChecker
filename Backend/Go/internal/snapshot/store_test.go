package snapshot

import (
	"archive/tar"
	"bytes"
	"compress/gzip"
	"context"
	"errors"
	"io"
	"log/slog"
	"os"
	"os/exec"
	"strings"
	"testing"
	"time"

	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/gitrepo"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/objectstore"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/testutil"
)

func newStore(t *testing.T, objects objectstore.Store, opts Options) *Store {
	t.Helper()
	if opts.Prefix == "" {
		opts.Prefix = "snapshots"
	}
	opts.WorkDir = t.TempDir()
	return NewStore(objects, gitrepo.Git{}, opts, slog.New(slog.NewTextHandler(io.Discard, nil)))
}

func sourceRepo(t *testing.T) string {
	return testutil.NewRepo(t,
		testutil.Commit{Author: "Alice", Email: "alice@example.com", Date: "2026-01-01T10:00:00Z", File: "README.md", Body: "# demo"},
		testutil.Commit{Author: "Bob", Email: "bob@example.com", Date: "2026-01-02T11:00:00Z", File: "main.go", Body: "package main // TODO: secret code"},
		testutil.Commit{Author: "Alice", Email: "alice@example.com", Date: "2026-01-02T12:00:00Z", File: "main.go", Body: "package main"},
	)
}

func TestCreateOpenDeleteRoundTrip(t *testing.T) {
	objects := objectstore.NewMemory()
	store := newStore(t, objects, Options{TTL: time.Hour})
	ctx := context.Background()
	req := CreateRequest{RepositoryID: "repo-1", RunID: "run-1", Clone: gitrepo.CloneOptions{URL: sourceRepo(t), Branch: "main"}}

	info, created, err := store.Create(ctx, req)
	if err != nil {
		t.Fatal(err)
	}
	if !created || info.RepoBytes == 0 || info.ArchiveBytes == 0 {
		t.Fatalf("info=%+v created=%v", info, created)
	}
	if keys := objects.Keys(); len(keys) != 1 || keys[0] != "snapshots/repo-1/run-1/repo.tar.gz" {
		t.Fatalf("keys = %v", keys)
	}
	if !info.ExpiresAt.Equal(info.CreatedAt.Add(time.Hour)) {
		t.Fatalf("expiresAt=%v createdAt=%v", info.ExpiresAt, info.CreatedAt)
	}

	// Повтор с тем же runId не клонирует заново.
	again, created, err := store.Create(ctx, req)
	if err != nil || created || again.ArchiveBytes != info.ArchiveBytes {
		t.Fatalf("second create: info=%+v created=%v err=%v", again, created, err)
	}

	ws, err := store.Open(ctx, "repo-1", "run-1")
	if err != nil {
		t.Fatal(err)
	}
	var authors []string
	err = gitrepo.Git{}.WalkCommits(ctx, ws.RepoDir, func(c gitrepo.Commit) error {
		authors = append(authors, c.AuthorName)
		return nil
	})
	if err != nil {
		t.Fatal(err)
	}
	if strings.Join(authors, ",") != "Alice,Bob,Alice" {
		t.Fatalf("authors = %v", authors)
	}
	dir := ws.RepoDir
	_ = ws.Close()
	if _, err := os.Stat(dir); !os.IsNotExist(err) {
		t.Fatalf("workspace not removed: %v", err)
	}

	if err := store.Delete(ctx, "repo-1", "run-1"); err != nil {
		t.Fatal(err)
	}
	if len(objects.Keys()) != 0 {
		t.Fatalf("object not deleted: %v", objects.Keys())
	}
	if err := store.Delete(ctx, "repo-1", "run-1"); err != nil {
		t.Fatalf("second delete must be idempotent: %v", err)
	}
	if _, err := store.Open(ctx, "repo-1", "run-1"); !errors.Is(err, ErrNotFound) {
		t.Fatalf("open after delete: %v", err)
	}
}

func TestPartialCloneHasNoFileContents(t *testing.T) {
	src := sourceRepo(t)
	if out, err := exec.Command("git", "-C", src, "config", "uploadpack.allowFilter", "true").CombinedOutput(); err != nil {
		t.Fatalf("%v: %s", err, out)
	}
	objects := objectstore.NewMemory()
	store := newStore(t, objects, Options{})
	ctx := context.Background()
	_, _, err := store.Create(ctx, CreateRequest{
		RepositoryID: "repo-1", RunID: "run-1",
		// file:// — чтобы git шёл через upload-pack и применил --filter=blob:none.
		Clone: gitrepo.CloneOptions{URL: "file://" + src, Branch: "main"},
	})
	if err != nil {
		t.Fatal(err)
	}

	ws, err := store.Open(ctx, "repo-1", "run-1")
	if err != nil {
		t.Fatal(err)
	}
	defer ws.Close()
	out, err := exec.Command("git", "-C", ws.RepoDir, "cat-file", "--batch-all-objects", "--batch-check=%(objecttype)").Output()
	if err != nil {
		t.Fatal(err)
	}
	if bytes.Contains(out, []byte("blob")) || !bytes.Contains(out, []byte("commit")) {
		t.Fatalf("snapshot objects:\n%s", out)
	}
}

func TestExpiredSnapshotsAreHiddenAndReaped(t *testing.T) {
	objects := objectstore.NewMemory()
	store := newStore(t, objects, Options{TTL: time.Hour})
	ctx := context.Background()
	src := sourceRepo(t)

	for _, run := range []string{"old", "fresh"} {
		if _, _, err := store.Create(ctx, CreateRequest{RepositoryID: "repo-1", RunID: run, Clone: gitrepo.CloneOptions{URL: src}}); err != nil {
			t.Fatal(err)
		}
		if run == "old" {
			store.now = func() time.Time { return time.Now().Add(90 * time.Minute) }
		}
	}
	// Посторонний объект без метаданных под префиксом тоже подчищается по времени изменения.
	objects.Now = func() time.Time { return time.Now().Add(-2 * time.Hour) }
	_, _ = objects.Put(ctx, "snapshots/stray/object", strings.NewReader("x"), nil)

	if _, err := store.Stat(ctx, "repo-1", "old"); !errors.Is(err, ErrNotFound) {
		t.Fatalf("expired snapshot visible: %v", err)
	}

	n, err := store.Reap(ctx)
	if err != nil {
		t.Fatal(err)
	}
	if n != 2 {
		t.Fatalf("reaped %d, want 2 (keys left: %v)", n, objects.Keys())
	}
	if keys := objects.Keys(); len(keys) != 1 || !strings.Contains(keys[0], "/fresh/") {
		t.Fatalf("keys = %v", keys)
	}
}

func TestRejectsUnsafeIDs(t *testing.T) {
	store := newStore(t, objectstore.NewMemory(), Options{})
	for _, id := range []string{"", "../x", "a/b", ".hidden", strings.Repeat("a", 200)} {
		if _, err := store.Stat(context.Background(), id, "run"); !errors.Is(err, ErrInvalidID) {
			t.Errorf("repo id %q: err = %v", id, err)
		}
	}
}

func TestSizeLimit(t *testing.T) {
	store := newStore(t, objectstore.NewMemory(), Options{MaxRepoBytes: 10})
	_, _, err := store.Create(context.Background(), CreateRequest{RepositoryID: "r", RunID: "x", Clone: gitrepo.CloneOptions{URL: sourceRepo(t)}})
	if !errors.Is(err, ErrTooLarge) {
		t.Fatalf("err = %v, want ErrTooLarge", err)
	}
}

func TestExtractRejectsPathTraversal(t *testing.T) {
	var buf bytes.Buffer
	gz := gzip.NewWriter(&buf)
	tw := tar.NewWriter(gz)
	_ = tw.WriteHeader(&tar.Header{Name: "../evil", Typeflag: tar.TypeReg, Size: 1, Mode: 0o644})
	_, _ = tw.Write([]byte("x"))
	_ = tw.Close()
	_ = gz.Close()

	err := extractArchive(&buf, t.TempDir(), 0)
	if err == nil || !strings.Contains(err.Error(), "unsafe path") {
		t.Fatalf("err = %v", err)
	}
}
