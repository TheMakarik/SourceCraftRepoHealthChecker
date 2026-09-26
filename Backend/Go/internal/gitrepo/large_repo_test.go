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

const (
	// defaultSyntheticFiles/Commits — размер, который проверяется в обычном прогоне без env.
	defaultSyntheticFiles   = 2000
	defaultSyntheticCommits = 50
	// heavySyntheticFiles/Commits — крупный репозиторий из ТЗ, включается LARGE_REPO_TESTS=1.
	heavySyntheticFiles   = 10000
	heavySyntheticCommits = 20000
	// generousTimeout — верхняя граница на построение и анализ синтетического репозитория.
	generousTimeout = 5 * time.Minute
	// realRepoCommandTimeout — граница одной git-команды на настоящем крупном репозитории.
	realRepoCommandTimeout = 5 * time.Minute
	// realRepoOverallTimeout — граница всего прогона по настоящему крупному репозиторию.
	realRepoOverallTimeout = 30 * time.Minute
)

// TestSyntheticRepositoryStreaming всегда проверяет связку ListFiles/WalkCommits/MarkerStats/DirSize
// на репозитории из 2000 файлов. Тяжёлый вариант (10 000 файлов / 20 000 коммитов) — в
// TestLargeRepositoryStreamingAndSizeLimit под LARGE_REPO_TESTS=1.
func TestSyntheticRepositoryStreaming(t *testing.T) {
	runSyntheticRepository(t, defaultSyntheticFiles, defaultSyntheticCommits)
}

func TestLargeRepositoryStreamingAndSizeLimit(t *testing.T) {
	if os.Getenv("LARGE_REPO_TESTS") != "1" {
		t.Skip("set LARGE_REPO_TESTS=1 to run the 10k-file / 20k-commit repository test")
	}
	files := largeRepoEnvInt(t, "LARGE_REPO_FILES", heavySyntheticFiles)
	commits := largeRepoEnvInt(t, "LARGE_REPO_COMMITS", heavySyntheticCommits)
	runSyntheticRepository(t, files, commits)
}

func runSyntheticRepository(t *testing.T, files, commits int) {
	t.Helper()
	if _, err := exec.LookPath("git"); err != nil {
		t.Skip("git not installed")
	}
	if files < 1 {
		files = 1
	}
	if commits < 1 {
		commits = 1
	}

	ctx, cancel := context.WithTimeout(context.Background(), generousTimeout)
	defer cancel()

	repositoryDirectory := buildLargeRepository(t, files, commits)
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

	elapsed := time.Since(started)
	if elapsed > generousTimeout {
		t.Errorf("analysis took %s, timeout is %s", elapsed, generousTimeout)
	}
	t.Logf("synthetic repository: files=%d commits=%d size=%dB todo=%d fixme=%d elapsed=%s",
		files, commits, size, markers.TodoCount, markers.FixmeCount, elapsed)
}

// TestRealLargeRepository проверяет потоковые git-операции на настоящем крупном
// репозитории. Запускается только при LARGE_REPO_PATH=/путь/к/repo.git (read-only).
//
// Репозиторий может быть blobless-клоном (partial clone): git log -G тогда лениво
// подтягивает отсутствующие объекты из promisor-remote и не успевает за разумное время.
// Так как каждая git-команда ограничена Git.Timeout, MarkerStats обязан завершиться
// по дедлайну, а не висеть бесконечно — это и проверяется (вместе с реальными числами
// ListFiles/WalkCommits/DirSize).
func TestRealLargeRepository(t *testing.T) {
	repoPath := os.Getenv("LARGE_REPO_PATH")
	if repoPath == "" {
		t.Skip("set LARGE_REPO_PATH=/path/to/large/repo.git to run against a real large repository")
	}
	info, err := os.Stat(repoPath)
	if err != nil {
		t.Fatalf("LARGE_REPO_PATH %q: %v", repoPath, err)
	}
	if !info.IsDir() {
		t.Fatalf("LARGE_REPO_PATH %q is not a directory", repoPath)
	}
	if _, err := exec.LookPath("git"); err != nil {
		t.Skip("git not installed")
	}

	commandTimeout := largeRepoEnvDuration(t, "LARGE_REPO_COMMAND_TIMEOUT", realRepoCommandTimeout)
	ctx, cancel := context.WithTimeout(context.Background(), realRepoOverallTimeout)
	defer cancel()

	size, err := gitrepo.DirSize(repoPath)
	if err != nil {
		t.Fatalf("DirSize: %v", err)
	}
	t.Logf("real repository: path=%s size=%dB (%.1f MiB)", repoPath, size, float64(size)/(1<<20))

	git := gitrepo.Git{Timeout: commandTimeout}

	started := time.Now()
	files, err := git.ListFiles(ctx, repoPath)
	if err != nil {
		t.Fatalf("ListFiles: %v", err)
	}
	if len(files) < 10_000 {
		t.Fatalf("ListFiles returned %d files, want >= 10000", len(files))
	}
	t.Logf("ListFiles: files=%d elapsed=%s", len(files), time.Since(started))

	started = time.Now()
	commits := 0
	err = git.WalkCommits(ctx, repoPath, func(gitrepo.Commit) error {
		commits++
		return nil
	})
	if err != nil {
		t.Fatalf("WalkCommits: %v", err)
	}
	if commits < 20_000 {
		t.Fatalf("WalkCommits visited %d commits, want >= 20000", commits)
	}
	t.Logf("WalkCommits: commits=%d elapsed=%s", commits, time.Since(started))

	started = time.Now()
	markers, err := git.MarkerStats(ctx, repoPath)
	elapsed := time.Since(started)
	switch {
	case err == nil:
		t.Logf("MarkerStats: todo=%d fixme=%d oldest=%v elapsed=%s", markers.TodoCount, markers.FixmeCount, markers.OldestAt, elapsed)
	case errors.Is(err, context.DeadlineExceeded), errors.Is(err, context.Canceled):
		// Partial clone мог лениво тянуть объекты: проверяем, что граница сработала,
		// а не что команда зависла.
		if elapsed > commandTimeout+2*time.Minute {
			t.Fatalf("MarkerStats ran %s, command timeout is %s: bound not enforced", elapsed, commandTimeout)
		}
		t.Logf("MarkerStats bounded by %s timeout after %s (partial clone may lazily fetch objects)", commandTimeout, elapsed)
	default:
		t.Fatalf("MarkerStats: %v", err)
	}
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
	// Отключаем фоновый auto-gc: иначе git переупаковывает objects прямо во время проверок
	// и DirSize/clone спотыкаются о transient-исчезновение файлов.
	runLargeRepoGit(t, directory, "config", "gc.auto", "0")
	runLargeRepoGit(t, directory, "config", "gc.autoDetach", "false")
	runLargeRepoGit(t, directory, "config", "maintenance.auto", "false")

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

func largeRepoEnvDuration(t *testing.T, name string, fallback time.Duration) time.Duration {
	t.Helper()
	value := os.Getenv(name)
	if value == "" {
		return fallback
	}
	parsed, err := time.ParseDuration(value)
	if err != nil {
		t.Fatalf("%s = %q: %v", name, value, err)
	}
	return parsed
}
