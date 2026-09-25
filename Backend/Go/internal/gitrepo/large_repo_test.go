package gitrepo_test

import (
	"context"
	"errors"
	"fmt"
	"io"
	"log/slog"
	"os"
	"os/exec"
	"path/filepath"
	"strconv"
	"testing"
	"time"

	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/gitrepo"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/objectstore"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/snapshot"
)

func TestLargeRepositoryStreamingAndSizeLimit(t *testing.T) {
	if os.Getenv("LARGE_REPO_TESTS") != "1" {
		t.Skip("set LARGE_REPO_TESTS=1 to run large repository tests")
	}
	if _, err := exec.LookPath("git"); err != nil {
		t.Skip("git not installed")
	}

	files := largeRepoEnvInt(t, "LARGE_REPO_FILES", 10000)
	commits := largeRepoEnvInt(t, "LARGE_REPO_COMMITS", 1)

	repositoryDirectory := buildLargeRepository(t, files, commits)
	ctx := context.Background()
	var git gitrepo.Git

	started := time.Now()

	listed, err := git.ListFiles(ctx, repositoryDirectory)
	if err != nil {
		t.Fatalf("ListFiles: %v", err)
	}
	if len(listed) != files {
		t.Fatalf("ListFiles returned %d files, want %d", len(listed), files)
	}

	walked := 0
	if err := git.WalkCommits(ctx, repositoryDirectory, func(gitrepo.Commit) error {
		walked++
		return nil
	}); err != nil {
		t.Fatalf("WalkCommits: %v", err)
	}
	if walked != commits {
		t.Fatalf("WalkCommits visited %d commits, want %d", walked, commits)
	}

	markers, err := git.MarkerStats(ctx, repositoryDirectory)
	if err != nil {
		t.Fatalf("MarkerStats: %v", err)
	}
	wantTodo := (files + 9) / 10
	wantFixme := (files + 24) / 25
	if markers.TodoCount != wantTodo {
		t.Errorf("TodoCount = %d, want %d", markers.TodoCount, wantTodo)
	}
	if markers.FixmeCount != wantFixme {
		t.Errorf("FixmeCount = %d, want %d", markers.FixmeCount, wantFixme)
	}
	if !markers.HasOldest {
		t.Error("HasOldest = false, want true")
	}

	size, err := gitrepo.DirSize(repositoryDirectory)
	if err != nil {
		t.Fatalf("DirSize: %v", err)
	}
	if size <= 0 {
		t.Fatalf("DirSize = %d, want > 0", size)
	}

	if err := assertSnapshotRejectsTooLarge(t, repositoryDirectory); err != nil {
		t.Fatal(err)
	}

	t.Logf("large repository: files=%d commits=%d size=%dB todo=%d fixme=%d elapsed=%s",
		files, commits, size, markers.TodoCount, markers.FixmeCount, time.Since(started))
}

func assertSnapshotRejectsTooLarge(t *testing.T, repositoryDirectory string) error {
	t.Helper()
	store := snapshot.NewStore(
		objectstore.NewMemory(),
		gitrepo.Git{},
		snapshot.Options{Prefix: "snapshots", TTL: time.Hour, WorkDir: t.TempDir(), MaxRepoBytes: 1},
		slog.New(slog.NewTextHandler(io.Discard, nil)),
	)

	_, _, err := store.Create(context.Background(), snapshot.CreateRequest{
		RepositoryID: "large-repository",
		RunID:        "run",
		Clone:        gitrepo.CloneOptions{URL: repositoryDirectory, Branch: "main"},
	})
	if !errors.Is(err, snapshot.ErrTooLarge) {
		return fmt.Errorf("Create with MaxRepoBytes=1: err = %v, want ErrTooLarge", err)
	}
	return nil
}

func buildLargeRepository(t *testing.T, files, commits int) string {
	t.Helper()
	directory := t.TempDir()
	runLargeRepoGit(t, directory, "init", "--quiet", "--initial-branch=main")
	runLargeRepoGit(t, directory, "config", "user.name", "Large Repository Author")
	runLargeRepoGit(t, directory, "config", "user.email", "large-repository@example.com")
	runLargeRepoGit(t, directory, "config", "commit.gpgsign", "false")

	for index := 0; index < files; index++ {
		batchDirectory := filepath.Join(directory, "src", fmt.Sprintf("batch-%05d", index/500))
		if err := os.MkdirAll(batchDirectory, 0o755); err != nil {
			t.Fatalf("MkdirAll %s: %v", batchDirectory, err)
		}

		content := fmt.Sprintf("package large\n\n// file %d\nvar Value%d = %d\n", index, index, index)
		if index%10 == 0 {
			content += "// TODO: refactor\n"
		}
		if index%25 == 0 {
			content += "// FIXME: fix\n"
		}

		path := filepath.Join(batchDirectory, fmt.Sprintf("file-%06d.go", index))
		if err := os.WriteFile(path, []byte(content), 0o644); err != nil {
			t.Fatalf("WriteFile %s: %v", path, err)
		}
	}

	runLargeRepoGit(t, directory, "add", "-A")
	runLargeRepoGit(t, directory, "commit", "--quiet", "--no-gpg-sign", "-m", "initial large import")
	for index := 1; index < commits; index++ {
		runLargeRepoGit(t, directory, "commit", "--quiet", "--no-gpg-sign", "--allow-empty", "-m", fmt.Sprintf("empty %d", index))
	}
	return directory
}

func runLargeRepoGit(t *testing.T, directory string, args ...string) {
	t.Helper()
	command := exec.Command("git", args...)
	command.Dir = directory
	command.Env = append(os.Environ(), "GIT_CONFIG_NOSYSTEM=1", "HOME="+directory)
	if output, err := command.CombinedOutput(); err != nil {
		t.Fatalf("git %v: %v\n%s", args, err, output)
	}
}

func largeRepoEnvInt(t *testing.T, name string, fallback int) int {
	t.Helper()
	value := os.Getenv(name)
	if value == "" {
		return fallback
	}
	parsed, err := strconv.Atoi(value)
	if err != nil {
		t.Fatalf("%s = %q: %v", name, value, err)
	}
	return parsed
}
