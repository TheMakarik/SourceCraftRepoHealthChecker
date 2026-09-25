package service

import (
	"context"
	"errors"
	"maps"
	"regexp"
	"slices"
	"sync"
	"time"

	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/contract"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/gitrepo"
)

// debtPattern — маркеры технического долга как отдельные слова в верхнем регистре (TODOS, todo_list не считаются).
const debtPattern = `(^|[^A-Za-z0-9_])(TODO|FIXME|HACK|XXX)([^A-Za-z0-9_]|$)`

var debtMarkerRe = regexp.MustCompile(`(?:^|[^A-Za-z0-9_])(TODO|FIXME|HACK|XXX)(?:[^A-Za-z0-9_]|$)`)

// debtExcludes — сторонний и сгенерированный код не относится к долгу проекта.
var debtExcludes = []string{
	"**/vendor/**", "**/node_modules/**", "**/third_party/**", "**/thirdparty/**",
	"**/*.min.js", "**/*.min.css", "**/*.lock", "**/package-lock.json", "**/go.sum",
	"**/*.svg", "**/*.map",
}

const (
	// maxBlameLinesPerFile ограничивает `git blame -L` на файл: за этим порогом возраст берётся по выборке.
	maxBlameLinesPerFile = 200
	// maxBlameFiles ограничивает число файлов для blame (~8 мс на файл без истории, больше — с глубокой историей).
	// В крупных репозиториях возраст самого старого маркера оценивается по равномерной выборке файлов.
	maxBlameFiles    = 500
	blameConcurrency = 4
)

// CodeHealth считает TODO/FIXME/HACK/XXX в ветке по умолчанию и возраст самого старого по git blame.
// Нужна локальная копия с блобами и историей (снапшот с withBlobs=true без depth).
func (s *Service) CodeHealth(ctx context.Context, token, repoID, runID string) (contract.CodeHealthReport, error) {
	var report contract.CodeHealthReport
	err := s.withRepo(ctx, token, repoID, runID, repoNeeds{Blobs: true}, func(dir string) error {
		linesByFile := map[string][]int{}
		err := s.git.Grep(ctx, dir, "HEAD", debtPattern, debtExcludes, func(m gitrepo.GrepMatch) error {
			found := false
			for _, sub := range debtMarkerRe.FindAllStringSubmatch(m.Text, -1) {
				found = true
				report.TotalCommentCount++
				switch sub[1] {
				case "TODO":
					report.TodoCount++
				case "FIXME":
					report.FixmeCount++
				}
			}
			if found {
				linesByFile[m.Path] = append(linesByFile[m.Path], m.Line)
			}
			return nil
		})
		if err != nil || len(linesByFile) == 0 {
			return err
		}

		oldest, err := s.oldestLine(ctx, dir, linesByFile)
		if err != nil {
			return err
		}
		if !oldest.IsZero() {
			age := contract.Duration(s.now().Sub(oldest))
			report.OldestCommentAge = &age
		}
		return nil
	})
	if errors.Is(err, ErrEmptyRepository) {
		return report, nil
	}
	return report, err
}

// oldestLine находит самое раннее время авторства среди строк с маркерами (blame по файлам параллельно).
func (s *Service) oldestLine(ctx context.Context, dir string, linesByFile map[string][]int) (time.Time, error) {
	ctx, cancel := context.WithCancel(ctx)
	defer cancel()

	var (
		mu       sync.Mutex
		oldest   time.Time
		firstErr error
		wg       sync.WaitGroup
		sem      = make(chan struct{}, blameConcurrency)
	)
	paths := slices.Sorted(maps.Keys(linesByFile))
	if len(paths) > maxBlameFiles {
		paths = sampleEvenlyStrings(paths, maxBlameFiles)
	}
	for _, path := range paths {
		lines := linesByFile[path]
		if len(lines) > maxBlameLinesPerFile {
			lines = sampleEvenly(lines, maxBlameLinesPerFile)
		}
		select {
		case sem <- struct{}{}:
		case <-ctx.Done():
		}
		if ctx.Err() != nil {
			break
		}
		wg.Go(func() {
			defer func() { <-sem }()
			times, err := s.git.BlameTimes(ctx, dir, "HEAD", path, lines)
			mu.Lock()
			defer mu.Unlock()
			if err != nil {
				if firstErr == nil {
					firstErr = err
					cancel()
				}
				return
			}
			for _, t := range times {
				if oldest.IsZero() || t.Before(oldest) {
					oldest = t
				}
			}
		})
	}
	wg.Wait()
	if firstErr != nil {
		return time.Time{}, firstErr
	}
	return oldest, ctx.Err()
}

// sampleEvenly выбирает n строк равномерно по файлу (первая и последняя всегда входят).
func sampleEvenly(lines []int, n int) []int {
	lines = slices.Clone(lines)
	slices.Sort(lines)
	out := make([]int, 0, n)
	for i := range n {
		out = append(out, lines[i*(len(lines)-1)/(n-1)])
	}
	return slices.Compact(out)
}

func sampleEvenlyStrings(items []string, n int) []string {
	out := make([]string, 0, n)
	for i := range n {
		out = append(out, items[i*(len(items)-1)/(n-1)])
	}
	return slices.Compact(out)
}
