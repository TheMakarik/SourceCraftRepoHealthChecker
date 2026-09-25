package service

import (
	"context"
	"errors"
	"path"
	"regexp"
	"slices"
	"strings"

	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/contract"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/gitrepo"
)

// Каталоги, где принято держать служебные файлы проекта (кроме корня).
var metaDirs = []string{"", "docs", ".sourcecraft", ".github", ".gitlab"}

const maxDocBytes = 1 << 20

// Documentation проверяет наличие ключевых файлов и инструкций в ветке по умолчанию.
func (s *Service) Documentation(ctx context.Context, token, repoID, runID string) (contract.DocumentationReport, error) {
	var report contract.DocumentationReport
	err := s.withRepo(ctx, token, repoID, runID, repoNeeds{Blobs: true, Depth: 1}, func(dir string) error {
		var readmes, contributings []string
		err := s.git.WalkFiles(ctx, dir, "HEAD", func(p string) error {
			switch classifyDocFile(p) {
			case docReadme:
				report.HasReadme = true
				readmes = append(readmes, p)
			case docLicense:
				report.HasLicense = true
			case docContributing:
				report.HasContributing = true
				contributings = append(contributings, p)
			case docCodeOwners:
				report.HasCodeOwners = true
			}
			return nil
		})
		if err != nil {
			return err
		}

		// Инструкции ищем в README (корневой — первым) и CONTRIBUTING.
		slices.SortFunc(readmes, func(a, b string) int { return strings.Count(a, "/") - strings.Count(b, "/") })
		for _, p := range append(readmes, contributings...) {
			data, err := s.git.ReadFile(ctx, dir, "HEAD", p, maxDocBytes)
			if errors.Is(err, gitrepo.ErrFileTooLarge) {
				continue
			}
			if err != nil {
				return err
			}
			run, buildAndTest := detectInstructions(string(data))
			report.HasLocalRunInstructions = report.HasLocalRunInstructions || run
			report.HasBuildAndTestInstructions = report.HasBuildAndTestInstructions || buildAndTest
		}
		return nil
	})
	return report, err
}

type docKind int

const (
	docOther docKind = iota
	docReadme
	docLicense
	docContributing
	docCodeOwners
)

// classifyDocFile определяет роль файла по имени и расположению (регистр не важен).
func classifyDocFile(p string) docKind {
	dir, file := path.Split(p)
	dir = strings.ToLower(strings.TrimSuffix(dir, "/"))
	if !slices.Contains(metaDirs, dir) {
		return docOther
	}
	stem := strings.ToLower(strings.TrimSuffix(file, path.Ext(file)))
	switch {
	case stem == "readme" && dir != ".gitlab":
		return docReadme
	case (stem == "license" || stem == "licence" || stem == "copying" || strings.HasPrefix(stem, "license-")) && dir == "":
		return docLicense
	case stem == "contributing":
		return docContributing
	case strings.ToLower(file) == "codeowners":
		return docCodeOwners
	default:
		return docOther
	}
}

var (
	headingRe   = regexp.MustCompile(`(?m)^\s{0,3}#{1,6}\s+(.+)$|^(.+)\n(?:=+|-+)\s*$`)
	codeFenceRe = regexp.MustCompile("(?s)```[^\\n]*\\n(.*?)```|~~~[^\\n]*\\n(.*?)~~~")

	runHeadingRe = regexp.MustCompile(`(?i)\b(install(ation|ing)?|getting started|quick ?start|usage|how to run|running|run locally|local (setup|development)|setup|deploy(ment)?)\b|запуск|установк|быстрый старт|начало работы|использован|развёртыван|развертыван`)
	runCmdRe     = regexp.MustCompile(`(?im)^\s*(\$\s*)?(docker(-| )compose\s+up|docker\s+run|npm\s+(start|run\s+(dev|start|serve))|yarn\s+(start|dev)|pnpm\s+(start|dev)|go\s+run|dotnet\s+run|cargo\s+run|python3?\s+\S+\.py|python3?\s+-m\s+\S+|uvicorn|flask\s+run|mvn\s+.*(spring-boot:run|exec:java)|gradlew?\s+(bootRun|run)|make\s+(run|start|up|serve)|java\s+-jar|bundle\s+exec\s+rails\s+s(erver)?|rails\s+s(erver)?|php\s+artisan\s+serve|\./(run|start)\S*)`)

	buildHeadingRe = regexp.MustCompile(`(?i)\b(build(ing)?|compil(e|ing|ation))\b|сборк|собрать|компиляц`)
	buildCmdRe     = regexp.MustCompile(`(?im)^\s*(\$\s*)?(go\s+build|dotnet\s+(build|publish)|cargo\s+build|npm\s+(run\s+build|ci|install)|yarn(\s+build)?\s*$|pnpm\s+(build|install)|mvn\s+(package|install|compile)|gradlew?\s+(build|assemble)|make(\s+(build|all))?\s*$|cmake\s|docker\s+build|pip\s+install|poetry\s+install)`)

	testHeadingRe = regexp.MustCompile(`(?i)\btest(s|ing)?\b|тест`)
	testCmdRe     = regexp.MustCompile(`(?im)^\s*(\$\s*)?(go\s+test|dotnet\s+test|cargo\s+test|npm\s+(test|run\s+test)|yarn\s+test|pnpm\s+test|mvn\s+(test|verify)|gradlew?\s+(test|check)|make\s+(test|check)|pytest|python3?\s+-m\s+(pytest|unittest)|tox|phpunit|rspec|bundle\s+exec\s+rspec|ctest)`)
)

// detectInstructions ищет инструкции запуска и сборки+тестирования в markdown-тексте.
// Сигнал засчитывается только в заголовке, за которым идёт код, или в команде внутри блока кода —
// слово «test» в произвольном абзаце не считается (устойчивость к накрутке одним словом).
func detectInstructions(md string) (run, buildAndTest bool) {
	var code strings.Builder
	for _, m := range codeFenceRe.FindAllStringSubmatch(md, -1) {
		code.WriteString(m[1])
		code.WriteString(m[2])
		code.WriteByte('\n')
	}
	codeText := code.String()
	hasCode := strings.TrimSpace(codeText) != ""

	var headings strings.Builder
	for _, m := range headingRe.FindAllStringSubmatch(md, -1) {
		headings.WriteString(m[1])
		headings.WriteString(m[2])
		headings.WriteByte('\n')
	}
	h := headings.String()

	run = runCmdRe.MatchString(codeText) || (hasCode && runHeadingRe.MatchString(h))
	build := buildCmdRe.MatchString(codeText) || (hasCode && buildHeadingRe.MatchString(h))
	test := testCmdRe.MatchString(codeText) || (hasCode && testHeadingRe.MatchString(h))
	return run, build && test
}
