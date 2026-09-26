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

// defaultCommandTimeout ограничивает одну git-команду, если Git.Timeout не задан.
// Так один огромный репозиторий (git log -G, git grep) не может подвесить анализ навсегда.
const defaultCommandTimeout = 5 * time.Minute

type Git struct {
	Binary string
	// Timeout ограничивает выполнение одной git-команды. 0 — defaultCommandTimeout.
	Timeout time.Duration
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
	ctx, cancel := g.commandContext(ctx)
	defer cancel()

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
		// Команда убита по таймауту/отмене — возвращаем причину контекста.
		if ctx.Err() != nil {
			return ctx.Err()
		}
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

// commandContext ограничивает одну git-команду: если у родительского ctx дедлайн раньше,
// используется он, иначе добавляется Git.Timeout (или defaultCommandTimeout).
func (g Git) commandContext(ctx context.Context) (context.Context, context.CancelFunc) {
	timeout := g.Timeout
	if timeout <= 0 {
		timeout = defaultCommandTimeout
	}
	if deadline, ok := ctx.Deadline(); ok && time.Until(deadline) <= timeout {
		return context.WithCancel(ctx)
	}
	return context.WithTimeout(ctx, timeout)
}

func (g Git) run(ctx context.Context, dir string, creds *Credentials, args ...string) ([]byte, error) {
	ctx, cancel := g.commandContext(ctx)
	defer cancel()

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

// runExit выполняет git и возвращает stdout вместе с кодом выхода, не превращая
// ненулевой код в ошибку (git grep возвращает 1, когда совпадений нет).
func (g Git) runExit(ctx context.Context, dir string, creds *Credentials, args ...string) ([]byte, int, error) {
	ctx, cancel := g.commandContext(ctx)
	defer cancel()

	cmd := g.command(ctx, dir, creds, args...)
	var stdout, stderr bytes.Buffer
	cmd.Stdout = &stdout
	cmd.Stderr = &limitedWriter{w: &stderr, n: 4 << 10}
	err := cmd.Run()
	if err == nil {
		return stdout.Bytes(), 0, nil
	}
	if ctx.Err() != nil {
		return nil, -1, ctx.Err()
	}
	var exitErr *exec.ExitError
	if errors.As(err, &exitErr) {
		return stdout.Bytes(), exitErr.ExitCode(), nil
	}
	return nil, -1, fmt.Errorf("git %s: %w: %s", args[0], err, redact(strings.TrimSpace(stderr.String()), creds))
}

// ListFiles возвращает пути всех отслеживаемых файлов ветки (из деревьев, без содержимого).
func (g Git) ListFiles(ctx context.Context, repoDir string) ([]string, error) {
	out, code, err := g.runExit(ctx, repoDir, nil, "ls-tree", "-r", "--name-only", "-z", "HEAD")
	if err != nil {
		return nil, err
	}
	if code != 0 {
		return nil, nil
	}
	raw := strings.Trim(string(out), "\x00")
	if raw == "" {
		return nil, nil
	}
	return strings.Split(raw, "\x00"), nil
}

// StructureStats — агрегированные показатели структуры каталогов.
type StructureStats struct {
	TotalFiles            int
	TotalDirectories      int
	MaxDepth              int
	RootFiles             int
	LargestDirectory      string
	LargestDirectoryFiles int
}

// Structure считает структуру папок по дереву файлов (без содержимого).
func (g Git) Structure(ctx context.Context, repoDir string) (StructureStats, error) {
	files, err := g.ListFiles(ctx, repoDir)
	if err != nil {
		return StructureStats{}, err
	}

	stats := StructureStats{TotalFiles: len(files)}
	directories := map[string]int{}

	for _, file := range files {
		idx := strings.LastIndexByte(file, '/')
		if idx < 0 {
			stats.RootFiles++
			continue
		}

		directories[file[:idx]]++
		if depth := strings.Count(file, "/"); depth > stats.MaxDepth {
			stats.MaxDepth = depth
		}
	}

	stats.TotalDirectories = len(directories)
	for directory, count := range directories {
		if count > stats.LargestDirectoryFiles {
			stats.LargestDirectoryFiles = count
			stats.LargestDirectory = directory
		}
	}

	return stats, nil
}

// ShowFile возвращает содержимое файла из HEAD.
func (g Git) ShowFile(ctx context.Context, repoDir, path string) ([]byte, error) {
	out, code, err := g.runExit(ctx, repoDir, nil, "show", "HEAD:"+path)
	if err != nil {
		return nil, err
	}
	if code != 0 {
		return nil, fmt.Errorf("git show %s: exit %d", path, code)
	}
	return out, nil
}

// MarkerStats — статистика маркеров TODO/FIXME и дата появления самого старого из них.
type MarkerStats struct {
	TodoCount  int
	FixmeCount int
	OldestAt   time.Time
	HasOldest  bool
}

// MarkerStats считает TODO/FIXME в ветке и определяет, когда появился самый старый маркер.
//
// Подсчёт приблизителен: git grep --word-regexp считает строки с маркером как отдельным
// словом на текущем HEAD. Давность — это минимальная author-дата коммитов, чей diff
// добавил или удалил строку с таким маркером (git log -G), то есть момент первого
// появления маркера в истории, а не обязательно текущей строки.
func (g Git) MarkerStats(ctx context.Context, repoDir string) (MarkerStats, error) {
	var stats MarkerStats

	hasHead, err := g.hasHead(ctx, repoDir)
	if err != nil {
		return stats, err
	}
	if !hasHead {
		return stats, nil
	}

	todo, err := g.markerCount(ctx, repoDir, "TODO")
	if err != nil {
		return stats, err
	}
	fixme, err := g.markerCount(ctx, repoDir, "FIXME")
	if err != nil {
		return stats, err
	}
	stats.TodoCount = todo
	stats.FixmeCount = fixme

	for _, word := range []string{"TODO", "FIXME"} {
		at, ok, err := g.oldestMarkerCommit(ctx, repoDir, word)
		if err != nil {
			return stats, err
		}
		if ok && (!stats.HasOldest || at.Before(stats.OldestAt)) {
			stats.OldestAt = at
			stats.HasOldest = true
		}
	}
	return stats, nil
}

func (g Git) hasHead(ctx context.Context, repoDir string) (bool, error) {
	_, code, err := g.runExit(ctx, repoDir, nil, "rev-parse", "--verify", "--quiet", "HEAD")
	if err != nil {
		return false, err
	}
	return code == 0, nil
}

func (g Git) markerCount(ctx context.Context, repoDir, word string) (int, error) {
	out, code, err := g.runExit(ctx, repoDir, nil, "grep", "-I", "-c", "--word-regexp", "-e", word, "HEAD")
	if err != nil {
		return 0, err
	}
	switch code {
	case 0:
	case 1:
		return 0, nil
	default:
		return 0, fmt.Errorf("git grep %s: exit %d", word, code)
	}

	total := 0
	sc := bufio.NewScanner(bytes.NewReader(out))
	for sc.Scan() {
		line := strings.TrimSpace(sc.Text())
		if line == "" {
			continue
		}
		idx := strings.LastIndexByte(line, ':')
		if idx < 0 {
			return 0, fmt.Errorf("git grep: unexpected record %q", truncate(line, 80))
		}
		n, err := strconv.Atoi(strings.TrimSpace(line[idx+1:]))
		if err != nil {
			return 0, fmt.Errorf("git grep: bad count %q: %w", line, err)
		}
		total += n
	}
	if err := sc.Err(); err != nil {
		return 0, err
	}
	return total, nil
}

func (g Git) oldestMarkerCommit(ctx context.Context, repoDir, word string) (time.Time, bool, error) {
	out, code, err := g.runExit(ctx, repoDir, nil, "log", "--format=%aI", "-G", `\b`+word+`\b`)
	if err != nil {
		return time.Time{}, false, err
	}
	if code != 0 {
		return time.Time{}, false, nil
	}

	var (
		oldest time.Time
		found  bool
	)
	sc := bufio.NewScanner(bytes.NewReader(out))
	for sc.Scan() {
		line := strings.TrimSpace(sc.Text())
		if line == "" {
			continue
		}
		at, err := time.Parse(time.RFC3339, line)
		if err != nil {
			return time.Time{}, false, fmt.Errorf("git log: bad date %q: %w", line, err)
		}
		if !found || at.Before(oldest) {
			oldest, found = at, true
		}
	}
	if err := sc.Err(); err != nil {
		return time.Time{}, false, err
	}
	return oldest, found, nil
}
