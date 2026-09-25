// Package testutil — общие хелперы тестов.
package testutil

import (
	"os"
	"os/exec"
	"path/filepath"
	"testing"
)

// Commit описывает коммит тестового репозитория.
type Commit struct {
	Author string
	Email  string
	Date   string // RFC3339
	File   string
	Body   string
}

// NewRepo создаёт git-репозиторий с ветки main и заданными коммитами и возвращает его путь.
func NewRepo(t *testing.T, commits ...Commit) string {
	t.Helper()
	if _, err := exec.LookPath("git"); err != nil {
		t.Skip("git not installed")
	}
	dir := t.TempDir()
	run(t, dir, nil, "init", "--quiet", "--initial-branch=main")
	for _, c := range commits {
		path := filepath.Join(dir, c.File)
		if err := os.MkdirAll(filepath.Dir(path), 0o755); err != nil {
			t.Fatal(err)
		}
		if err := os.WriteFile(path, []byte(c.Body), 0o644); err != nil {
			t.Fatal(err)
		}
		run(t, dir, nil, "add", "--", c.File)
		env := []string{
			"GIT_AUTHOR_NAME=" + c.Author, "GIT_AUTHOR_EMAIL=" + c.Email, "GIT_AUTHOR_DATE=" + c.Date,
			"GIT_COMMITTER_NAME=" + c.Author, "GIT_COMMITTER_EMAIL=" + c.Email, "GIT_COMMITTER_DATE=" + c.Date,
		}
		run(t, dir, env, "commit", "--quiet", "--no-gpg-sign", "-m", "update "+c.File)
	}
	return dir
}

func run(t *testing.T, dir string, env []string, args ...string) {
	t.Helper()
	cmd := exec.Command("git", args...)
	cmd.Dir = dir
	cmd.Env = append(os.Environ(), "GIT_CONFIG_NOSYSTEM=1", "HOME="+dir)
	cmd.Env = append(cmd.Env, env...)
	if out, err := cmd.CombinedOutput(); err != nil {
		t.Fatalf("git %v: %v\n%s", args, err, out)
	}
}
