// Package gitrepo работает с локальным git: компактный клон и потоковое чтение истории.
//
// Креды никогда не попадают в remote URL, argv или логи: заголовок авторизации
// передаётся через GIT_CONFIG_* переменные окружения процесса git.
package gitrepo

import (
	"bufio"
	"bytes"
	"context"
	"encoding/base64"
	"errors"
	"fmt"
	"io"
	"io/fs"
	"os"
	"os/exec"
	"path/filepath"
	"strconv"
	"strings"
	"time"
)

type Git struct {
	Binary string
}

// Credentials — basic-auth для git по HTTPS (логин SourceCraft + PAT).
type Credentials struct {
	Username string
	Token    string
}

// CloneOptions задаёт компактный формат клона (раздел 5 ТЗ).
type CloneOptions struct {
	URL    string
	Branch string
	// WithBlobs — полный клон ветки; иначе --filter=blob:none (только история и деревья).
	WithBlobs bool
	// Depth > 0 — shallow clone; 0 — вся история ветки.
	Depth int
	Creds *Credentials
}

// ErrRepoTooLarge — клон превысил допустимый размер.
var ErrRepoTooLarge = errors.New("gitrepo: repository exceeds size limit")

// CloneBare клонирует репозиторий в dir как bare, single-branch.
func (g Git) CloneBare(ctx context.Context, dir string, opts CloneOptions) error {
	args := []string{"clone", "--bare", "--single-branch", "--no-tags", "--quiet"}
	if opts.Branch != "" {
		args = append(args, "--branch", opts.Branch)
	}
	if !opts.WithBlobs {
		args = append(args, "--filter=blob:none")
	}
	if opts.Depth > 0 {
		args = append(args, "--depth", strconv.Itoa(opts.Depth))
	}
	args = append(args, "--", opts.URL, dir)

	_, err := g.run(ctx, "", opts.Creds, args...)
	return err
}

// DirSize возвращает суммарный размер файлов каталога.
func DirSize(dir string) (int64, error) {
	var total int64
	err := filepath.WalkDir(dir, func(_ string, d fs.DirEntry, err error) error {
		if err != nil {
			return err
		}
		if d.Type().IsRegular() {
			info, err := d.Info()
			if err != nil {
				return err
			}
			total += info.Size()
		}
		return nil
	})
	return total, err
}

// Commit — метаданные коммита без содержимого файлов.
type Commit struct {
	Hash        string
	AuthorName  string
	AuthorEmail string
	AuthoredAt  time.Time
}

const (
	fieldSep  = "\x1f"
	recordSep = "\x1e"
)

// WalkCommits потоково читает `git log` и вызывает fn для каждого коммита — без загрузки истории в память.
func (g Git) WalkCommits(ctx context.Context, repoDir string, fn func(Commit) error) error {
	cmd := g.command(ctx, repoDir, nil, "log", "--no-color", "--format="+recordSep+"%H"+fieldSep+"%aN"+fieldSep+"%aE"+fieldSep+"%aI")
	stdout, err := cmd.StdoutPipe()
	if err != nil {
		return err
	}
	var stderr bytes.Buffer
	cmd.Stderr = &limitedWriter{w: &stderr, n: 4 << 10}
	if err := cmd.Start(); err != nil {
		return err
	}

	parseErr := parseLog(stdout, fn)
	if parseErr != nil {
		_ = cmd.Process.Kill()
		_, _ = io.Copy(io.Discard, stdout)
	}
	waitErr := cmd.Wait()
	switch {
	case parseErr != nil:
		return parseErr
	case waitErr != nil:
		// Пустой репозиторий (нет коммитов) — не ошибка.
		if strings.Contains(stderr.String(), "does not have any commits") {
			return nil
		}
		return fmt.Errorf("git log: %w: %s", waitErr, strings.TrimSpace(stderr.String()))
	}
	return nil
}

func parseLog(r io.Reader, fn func(Commit) error) error {
	sc := bufio.NewScanner(r)
	sc.Buffer(make([]byte, 64<<10), 1<<20)
	for sc.Scan() {
		line := strings.TrimPrefix(sc.Text(), recordSep)
		if line == "" {
			continue
		}
		parts := strings.Split(line, fieldSep)
		if len(parts) != 4 {
			return fmt.Errorf("git log: unexpected record %q", truncate(line, 80))
		}
		at, err := time.Parse(time.RFC3339, parts[3])
		if err != nil {
			return fmt.Errorf("git log: bad date %q: %w", parts[3], err)
		}
		if err := fn(Commit{Hash: parts[0], AuthorName: parts[1], AuthorEmail: parts[2], AuthoredAt: at}); err != nil {
			return err
		}
	}
	return sc.Err()
}

func (g Git) run(ctx context.Context, dir string, creds *Credentials, args ...string) ([]byte, error) {
	cmd := g.command(ctx, dir, creds, args...)
	var stdout, stderr bytes.Buffer
	cmd.Stdout = &stdout
	cmd.Stderr = &limitedWriter{w: &stderr, n: 4 << 10}
	if err := cmd.Run(); err != nil {
		if ctx.Err() != nil {
			return nil, ctx.Err()
		}
		return nil, fmt.Errorf("git %s: %w: %s", args[0], err, redact(strings.TrimSpace(stderr.String()), creds))
	}
	return stdout.Bytes(), nil
}

func (g Git) command(ctx context.Context, dir string, creds *Credentials, args ...string) *exec.Cmd {
	bin := g.Binary
	if bin == "" {
		bin = "git"
	}
	cmd := exec.CommandContext(ctx, bin, args...)
	cmd.Dir = dir
	cmd.WaitDelay = 5 * time.Second
	cmd.Env = append(os.Environ(),
		"GIT_TERMINAL_PROMPT=0",
		"GIT_ASKPASS=",
		"SSH_ASKPASS=",
		"GIT_CONFIG_NOSYSTEM=1",
		"LC_ALL=C",
	)
	if creds != nil && creds.Token != "" {
		basic := base64.StdEncoding.EncodeToString([]byte(creds.Username + ":" + creds.Token))
		cmd.Env = append(cmd.Env,
			"GIT_CONFIG_COUNT=1",
			"GIT_CONFIG_KEY_0=http.extraHeader",
			"GIT_CONFIG_VALUE_0=Authorization: Basic "+basic,
		)
	}
	return cmd
}

func redact(s string, creds *Credentials) string {
	if creds != nil && creds.Token != "" {
		s = strings.ReplaceAll(s, creds.Token, "***")
	}
	return s
}

func truncate(s string, n int) string {
	if len(s) <= n {
		return s
	}
	return s[:n] + "…"
}

type limitedWriter struct {
	w io.Writer
	n int
}

func (l *limitedWriter) Write(p []byte) (int, error) {
	if l.n <= 0 {
		return len(p), nil
	}
	chunk := p
	if len(chunk) > l.n {
		chunk = chunk[:l.n]
	}
	l.n -= len(chunk)
	_, _ = l.w.Write(chunk)
	return len(p), nil
}
