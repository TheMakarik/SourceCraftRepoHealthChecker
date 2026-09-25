package gitrepo

import (
	"bufio"
	"bytes"
	"context"
	"errors"
	"fmt"
	"io"
	"os/exec"
	"slices"
	"strconv"
	"strings"
	"time"
)

// Операции над содержимым файлов ревизии. Работают по bare-репозиторию без checkout;
// содержимое читается потоком и наружу не отдаётся — только пути, номера строк и счётчики.

// WalkFiles вызывает fn для пути каждого отслеживаемого файла ревизии rev (`git ls-tree -r`). Блобы не нужны.
func (g Git) WalkFiles(ctx context.Context, repoDir, rev string, fn func(path string) error) error {
	return g.stream(ctx, repoDir, []string{"ls-tree", "-r", "-z", "--name-only", rev}, nil, func(r io.Reader) error {
		sc := bufio.NewScanner(r)
		sc.Buffer(make([]byte, 64<<10), 1<<20)
		sc.Split(splitNUL)
		for sc.Scan() {
			if err := fn(sc.Text()); err != nil {
				return err
			}
		}
		return sc.Err()
	})
}

// ErrFileTooLarge — файл больше допустимого для чтения размера.
var ErrFileTooLarge = errors.New("gitrepo: file too large")

// ReadFile читает файл rev:path целиком, но не больше maxBytes.
func (g Git) ReadFile(ctx context.Context, repoDir, rev, path string, maxBytes int64) ([]byte, error) {
	var out []byte
	err := g.stream(ctx, repoDir, []string{"cat-file", "blob", rev + ":" + path}, nil, func(r io.Reader) error {
		data, err := io.ReadAll(io.LimitReader(r, maxBytes+1))
		if err != nil {
			return err
		}
		if int64(len(data)) > maxBytes {
			return ErrFileTooLarge
		}
		out = data
		return nil
	})
	return out, err
}

// GrepMatch — строка, совпавшая с шаблоном.
type GrepMatch struct {
	Path string
	Line int
	Text string
}

// Grep ищет ERE-шаблон в текстовых файлах ревизии rev (`git grep -I`), пропуская excludes (pathspec-глобы).
// Отсутствие совпадений — не ошибка.
func (g Git) Grep(ctx context.Context, repoDir, rev, pattern string, excludes []string, fn func(GrepMatch) error) error {
	args := []string{"grep", "-z", "-n", "-I", "-E", "--no-color", "-e", pattern, rev, "--", "."}
	for _, ex := range excludes {
		args = append(args, ":(exclude,glob)"+ex)
	}
	prefix := rev + ":"
	return g.stream(ctx, repoDir, args, []int{1}, func(r io.Reader) error {
		br := bufio.NewReaderSize(r, 64<<10)
		for {
			path, err := br.ReadString(0)
			if errors.Is(err, io.EOF) {
				return nil
			}
			if err != nil {
				return err
			}
			lineNo, err := br.ReadString(0)
			if err != nil {
				return fmt.Errorf("git grep: truncated record: %w", err)
			}
			text, err := readLine(br, 4<<10)
			if err != nil && !errors.Is(err, io.EOF) {
				return err
			}
			n, convErr := strconv.Atoi(strings.TrimSuffix(lineNo, "\x00"))
			if convErr != nil {
				return fmt.Errorf("git grep: bad line number %q", truncate(lineNo, 20))
			}
			m := GrepMatch{Path: strings.TrimPrefix(strings.TrimSuffix(path, "\x00"), prefix), Line: n, Text: text}
			if err := fn(m); err != nil {
				return err
			}
		}
	})
}

// BlameTimes возвращает время авторства указанных строк файла (`git blame --line-porcelain -L`).
// Ключ — номер строки в ревизии rev. На shallow-клоне время ограничено границей истории.
func (g Git) BlameTimes(ctx context.Context, repoDir, rev, path string, lines []int) (map[int]time.Time, error) {
	if len(lines) == 0 {
		return map[int]time.Time{}, nil
	}
	args := []string{"blame", "--line-porcelain"}
	for _, n := range lines {
		args = append(args, "-L", strconv.Itoa(n)+","+strconv.Itoa(n))
	}
	args = append(args, rev, "--", path)

	times := make(map[int]time.Time, len(lines))
	err := g.stream(ctx, repoDir, args, nil, func(r io.Reader) error {
		sc := bufio.NewScanner(r)
		sc.Buffer(make([]byte, 64<<10), 1<<20)
		current := 0
		for sc.Scan() {
			line := sc.Text()
			switch {
			case strings.HasPrefix(line, "\t"):
				current = 0
			case strings.HasPrefix(line, "author-time "):
				if current == 0 {
					continue
				}
				secs, err := strconv.ParseInt(strings.TrimPrefix(line, "author-time "), 10, 64)
				if err != nil {
					return fmt.Errorf("git blame: bad author-time: %w", err)
				}
				times[current] = time.Unix(secs, 0).UTC()
			case current == 0 && isBlameHeader(line):
				// "<sha> <orig-line> <final-line> [<count>]"
				fields := strings.Fields(line)
				n, err := strconv.Atoi(fields[2])
				if err != nil {
					return fmt.Errorf("git blame: bad header")
				}
				current = n
			}
		}
		return sc.Err()
	})
	return times, err
}

func isBlameHeader(line string) bool {
	fields := strings.Fields(line)
	if len(fields) < 3 || (len(fields[0]) != 40 && len(fields[0]) != 64) {
		return false
	}
	for _, c := range fields[0] {
		if !strings.ContainsRune("0123456789abcdef", c) {
			return false
		}
	}
	return true
}

// stream запускает git и отдаёт stdout в consume. okCodes — коды выхода, которые не считаются ошибкой.
func (g Git) stream(ctx context.Context, dir string, args []string, okCodes []int, consume func(io.Reader) error) error {
	cmd := g.command(ctx, dir, nil, args...)
	stdout, err := cmd.StdoutPipe()
	if err != nil {
		return err
	}
	var stderr bytes.Buffer
	cmd.Stderr = &limitedWriter{w: &stderr, n: 4 << 10}
	if err := cmd.Start(); err != nil {
		return err
	}

	consumeErr := consume(stdout)
	if consumeErr != nil {
		_ = cmd.Process.Kill()
	}
	_, _ = io.Copy(io.Discard, stdout)
	waitErr := cmd.Wait()

	msg := strings.TrimSpace(stderr.String())
	switch {
	case consumeErr != nil:
		return consumeErr
	case ctx.Err() != nil:
		return ctx.Err()
	// Без lazy fetch git не падает на недостающем блобе partial clone, а пишет в stderr —
	// `git grep` при этом выходит с кодом 1, как при «нет совпадений». Проверяем до кодов выхода.
	case strings.Contains(msg, "unable to read") || strings.Contains(msg, ": bad file"):
		return ErrMissingObjects
	case waitErr != nil:
		var exitErr *exec.ExitError
		if errors.As(waitErr, &exitErr) && slices.Contains(okCodes, exitErr.ExitCode()) {
			return nil
		}
		if strings.Contains(msg, "does not have any commits") || strings.Contains(msg, "Not a valid object name") ||
			strings.Contains(msg, "not a tree object") || strings.Contains(msg, "invalid object name") {
			return ErrNoRevision
		}
		return fmt.Errorf("git %s: %w: %s", args[0], waitErr, truncate(msg, 300))
	}
	return nil
}

var (
	// ErrNoRevision — ревизии нет (например, пустой репозиторий).
	ErrNoRevision = errors.New("gitrepo: revision not found")
	// ErrMissingObjects — в локальной копии нет нужных объектов (partial clone без блобов).
	ErrMissingObjects = errors.New("gitrepo: objects missing from local copy")
)

// readLine читает строку до '\n', сохраняя не больше max байт (остаток строки отбрасывается).
func readLine(br *bufio.Reader, max int) (string, error) {
	var b strings.Builder
	for {
		chunk, isPrefix, err := br.ReadLine()
		if b.Len() < max {
			rest := max - b.Len()
			if len(chunk) > rest {
				chunk = chunk[:rest]
			}
			b.Write(chunk)
		}
		if err != nil {
			return b.String(), err
		}
		if !isPrefix {
			return b.String(), nil
		}
	}
}

func splitNUL(data []byte, atEOF bool) (advance int, token []byte, err error) {
	if i := bytes.IndexByte(data, 0); i >= 0 {
		return i + 1, data[:i], nil
	}
	if atEOF && len(data) > 0 {
		return len(data), data, nil
	}
	return 0, nil, nil
}
