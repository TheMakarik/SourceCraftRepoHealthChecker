// Package service собирает факты из SourceCraft API и снапшотов git и приводит их к контракту C#.
package service

import (
	"context"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"regexp"
	"strings"
	"sync"
	"time"

	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/appsec"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/contract"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/gitrepo"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/snapshot"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/sourcecraft"
)

type Limits struct {
	// MaxItems — сколько последних issues/MR/запусков CI забирать из API.
	MaxItems int
	// MaxResponseLookups — для скольких issues/MR запрашивать комментарии (время первого ответа).
	MaxResponseLookups int
	// Concurrency — параллельные запросы комментариев.
	Concurrency int
}

type Service struct {
	api         *sourcecraft.Client
	appSec      *appsec.Client
	snapshots   *snapshot.Store
	git         gitrepo.Git
	gitUsername string
	workDir     string
	limits      Limits
}

func New(api *sourcecraft.Client, appSec *appsec.Client, snapshots *snapshot.Store, git gitrepo.Git, gitUsername, workDir string, limits Limits) *Service {
	if limits.MaxItems <= 0 {
		limits.MaxItems = 500
	}
	if limits.MaxResponseLookups < 0 {
		limits.MaxResponseLookups = 0
	}
	if limits.Concurrency <= 0 {
		limits.Concurrency = 4
	}
	return &Service{api: api, appSec: appSec, snapshots: snapshots, git: git, gitUsername: gitUsername, workDir: workDir, limits: limits}
}

func (s *Service) CurrentUser(ctx context.Context, token string) (contract.SourceCraftUser, error) {
	u, err := s.api.CurrentUser(ctx, token)
	if err != nil {
		return contract.SourceCraftUser{}, err
	}
	// Публичный профиль SourceCraft не отдаёт email.
	return contract.SourceCraftUser{ID: u.ID, Login: u.Username, DisplayName: u.DisplayName}, nil
}

func (s *Service) MyRepositories(ctx context.Context, token string) ([]contract.SourceCraftRepository, error) {
	repos, err := s.api.MyRepositories(ctx, token)
	if err != nil {
		return nil, err
	}
	return mapSlice(repos, toRepository), nil
}

func (s *Service) Catalog(ctx context.Context, token, pageToken string, pageSize int, sortBy string) ([]contract.SourceCraftRepository, string, error) {
	page, err := s.api.DiscoverRepositories(ctx, token, pageToken, pageSize, sortBy)
	if err != nil {
		return nil, "", err
	}
	return mapSlice(page.Items, toRepository), page.NextPageToken, nil
}

func (s *Service) Repository(ctx context.Context, token, repoID string) (contract.SourceCraftRepository, error) {
	r, err := s.api.Repository(ctx, token, repoID)
	if err != nil {
		return contract.SourceCraftRepository{}, err
	}
	return toRepository(r), nil
}

func (s *Service) Releases(ctx context.Context, token, repoID string) ([]contract.ReleaseInfo, error) {
	releases, err := s.api.Releases(ctx, token, repoID)
	if err != nil {
		return nil, err
	}
	out := make([]contract.ReleaseInfo, 0, len(releases))
	for _, r := range releases {
		if r.Status != "published" {
			continue
		}
		published := r.CreatedAt
		if r.ReleasedAt != nil {
			published = *r.ReleasedAt
		}
		out = append(out, contract.ReleaseInfo{Name: r.Title, Tag: r.Tag, PublishedAt: published})
	}
	return out, nil
}

// SecurityFindings забирает находки AppSec SourceCraft для репозитория.
//
// Сначала id репозитория разрешается через SourceCraft (gitRepo в AppSec — это repo.ID),
// затем запрашиваются группы дефектов. Никакого собственного сканирования нет.
//
// AppSec отдаёт kind и severity целыми числами; порядок значений принят по OpenAPI
// (https://appsec.sourcecraft.tech/openapi) и при изменении контракта правится здесь:
//   - engineType: 0 SECRETS, 1 SCA, 2 SAST, 3 SBOM_SPDX, 4 SBOM_CYCLONEDX, 5 DAST, 6 AI_AUDIT;
//   - severity: 0 NONE, 1 LOW, 2 MEDIUM, 3 HIGH, 4 CRITICAL;
//   - status: 0 OPEN, дальше RESOLVED_*.
//
// Строковый engine имеет приоритет: SECRETS/SCA распознаются точнее, чем int.
// NONE маппится в Low: в контракте C# нет Severity.None. Статус, отличный от OPEN, — Fixed.
func (s *Service) SecurityFindings(ctx context.Context, token, repoID string) ([]contract.SecurityFinding, error) {
	repo, err := s.api.Repository(ctx, token, repoID)
	if err != nil {
		return nil, err
	}
	page, err := s.appSec.DefectGroups(ctx, token, repo.ID, 0, "")
	if err != nil {
		return nil, fmt.Errorf("appsec findings: %w", err)
	}
	out := make([]contract.SecurityFinding, 0, len(page.Items))
	for _, finding := range page.Items {
		out = append(out, toSecurityFinding(finding))
	}
	return out, nil
}

func toSecurityFinding(f appsec.DefectGroupDto) contract.SecurityFinding {
	id := f.UUID
	if id == "" {
		id = f.PublicID
	}
	title := f.RuleName
	if title == "" {
		title = f.RuleID
	}
	return contract.SecurityFinding{
		ID:       id,
		Kind:     toFindingKind(f.EngineType, f.Engine),
		Severity: toFindingSeverity(f.Severity),
		Status:   toFindingStatus(f.Status),
		Title:    title,
		Package:  optionalString(f.RuleID),
		FilePath: optionalString(f.FileName),
	}
}

func toFindingKind(engineType int32, engine string) contract.SecurityFindingKind {
	switch strings.ToUpper(engine) {
	case "SECRETS", "SECRET", "SECRET_SCANNING":
		return contract.SecurityFindingSecretScanning
	case "SCA":
		return contract.SecurityFindingSca
	}
	switch engineType {
	case 0:
		return contract.SecurityFindingSecretScanning
	case 1:
		return contract.SecurityFindingSca
	default:
		return contract.SecurityFindingSast
	}
}

func toFindingSeverity(severity int32) contract.SecuritySeverity {
	switch severity {
	case 2:
		return contract.SecuritySeverityMedium
	case 3:
		return contract.SecuritySeverityHigh
	case 4:
		return contract.SecuritySeverityCritical
	default:
		return contract.SecuritySeverityLow
	}
}

func toFindingStatus(status int32) contract.SecurityFindingStatus {
	if status == 0 {
		return contract.SecurityFindingOpen
	}
	return contract.SecurityFindingFixed
}

func optionalString(s string) *string {
	if s == "" {
		return nil
	}
	return &s
}

func (s *Service) Pipelines(ctx context.Context, token, repoID string) ([]contract.PipelineRun, error) {
	runs, err := s.api.Runs(ctx, token, repoID, s.limits.MaxItems)
	if err != nil {
		return nil, err
	}
	out := make([]contract.PipelineRun, 0, len(runs))
	for _, r := range runs {
		started := firstTime(r.Dates.StartedAt, r.Dates.CreatedAt)
		if started == nil {
			continue
		}
		id := r.ID
		if id == "" {
			id = r.Slug // у CI-сущностей SourceCraft пока нет публичных id
		}
		out = append(out, contract.PipelineRun{
			ID:         id,
			Status:     toPipelineStatus(r.Status),
			Branch:     "", // API запусков не отдаёт ветку
			StartedAt:  *started,
			FinishedAt: r.Dates.FinishedAt,
		})
	}
	return out, nil
}

func (s *Service) Issues(ctx context.Context, token, repoID string) ([]contract.IssueInfo, error) {
	issues, err := s.api.Issues(ctx, token, repoID, s.limits.MaxItems)
	if err != nil {
		return nil, err
	}
	out := make([]contract.IssueInfo, len(issues))
	for i, is := range issues {
		out[i] = contract.IssueInfo{
			ID:          is.ID,
			Title:       is.Title,
			State:       contract.IssueOpen,
			AuthorLogin: is.Author.Slug,
			CreatedAt:   is.CreatedAt,
			UpdatedAt:   is.UpdatedAt,
		}
		if st := is.Status.StatusType; st == "completed" || st == "cancelled" {
			out[i].State = contract.IssueClosed
			closed := is.UpdatedAt
			if is.CompletedAt != nil {
				closed = *is.CompletedAt
			}
			out[i].ClosedAt = &closed
		}
	}

	err = s.forEachLimited(ctx, min(len(issues), s.limits.MaxResponseLookups), func(ctx context.Context, i int) error {
		comments, err := s.api.IssueComments(ctx, token, issues[i].ID, 20)
		if err != nil {
			return err
		}
		for _, c := range comments {
			if c.Author.ID != issues[i].Author.ID {
				at := c.CreatedAt
				out[i].FirstResponseAt = &at
				break
			}
		}
		return nil
	})
	return out, err
}

func (s *Service) MergeRequests(ctx context.Context, token, repoID string) ([]contract.MergeRequestInfo, error) {
	prs, err := s.api.PullRequests(ctx, token, repoID, s.limits.MaxItems)
	if err != nil {
		return nil, err
	}
	out := make([]contract.MergeRequestInfo, len(prs))
	for i, pr := range prs {
		out[i] = contract.MergeRequestInfo{
			ID:          pr.ID,
			Title:       pr.Title,
			State:       contract.MergeRequestOpen,
			AuthorLogin: pr.Author.Slug,
			CreatedAt:   pr.CreatedAt,
		}
		// API не отдаёт отдельные merged_at/closed_at — для завершённых PR берём updated_at.
		updated := pr.UpdatedAt
		switch pr.Status {
		case "merged":
			out[i].State = contract.MergeRequestMerged
			out[i].MergedAt = &updated
			out[i].ClosedAt = &updated
		case "discarded":
			out[i].State = contract.MergeRequestClosed
			out[i].ClosedAt = &updated
		}
	}

	err = s.forEachLimited(ctx, min(len(prs), s.limits.MaxResponseLookups), func(ctx context.Context, i int) error {
		comments, err := s.api.PullRequestComments(ctx, token, prs[i].ID, 0)
		if err != nil {
			return err
		}
		for _, c := range comments {
			if c.IsDeleted || !c.IsPublished || c.Author.ID == prs[i].Author.ID {
				continue
			}
			out[i].ReviewCommentsCount++
			if out[i].FirstResponseAt == nil {
				at := c.CreatedAt
				out[i].FirstResponseAt = &at
			}
		}
		return nil
	})
	return out, err
}

// forEachLimited выполняет fn(i) для i в [0, n) не более чем в limits.Concurrency горутинах.
//
// Токен семафора захватывается до запуска горутины и освобождается ровно один раз:
// либо самой горутиной, либо (если ctx отменился сразу после захвата) этим циклом.
// Поэтому отмена ctx не теряет токены и не запускает задачи в мёртвый контекст.
// Первая ошибка отменяет остальные вызовы и возвращается вызывающему, а wg.Wait
// гарантирует, что после возврата не осталось работающих горутин.
func (s *Service) forEachLimited(ctx context.Context, n int, fn func(ctx context.Context, i int) error) error {
	if n <= 0 {
		return nil
	}
	concurrency := s.limits.Concurrency
	if concurrency <= 0 {
		concurrency = 1
	}

	ctx, cancel := context.WithCancel(ctx)
	defer cancel()

	var (
		wg       sync.WaitGroup
		once     sync.Once
		firstErr error
		sem      = make(chan struct{}, concurrency)
	)
loop:
	for i := range n {
		select {
		case <-ctx.Done():
			break loop
		case sem <- struct{}{}:
		}

		// Токен захвачен. Если контекст отменился между select и запуском, освобождаем
		// токен сами: горутина не стартует, поэтому освобождать его будет некому.
		if ctx.Err() != nil {
			<-sem
			break loop
		}

		wg.Go(func() {
			defer func() { <-sem }()
			if err := fn(ctx, i); err != nil {
				once.Do(func() {
					firstErr = err
					cancel()
				})
			}
		})
	}
	wg.Wait()
	if firstErr != nil {
		return firstErr
	}
	return ctx.Err()
}

// SnapshotRequest — параметры создания снапшота.
type SnapshotRequest struct {
	RepositoryID string
	RunID        string
	WithBlobs    bool
	Depth        int
}

// CreateSnapshot проверяет доступ пользователя к репозиторию, клонирует его и кладёт снапшот в S3.
func (s *Service) CreateSnapshot(ctx context.Context, token string, req SnapshotRequest) (snapshot.Info, bool, error) {
	repo, err := s.api.Repository(ctx, token, req.RepositoryID)
	if err != nil {
		return snapshot.Info{}, false, err
	}
	if repo.IsEmpty {
		return snapshot.Info{}, false, ErrEmptyRepository
	}
	creds, err := s.gitCredentials(ctx, token)
	if err != nil {
		return snapshot.Info{}, false, err
	}
	return s.snapshots.Create(ctx, snapshot.CreateRequest{
		RepositoryID: repo.ID,
		RunID:        req.RunID,
		Clone: gitrepo.CloneOptions{
			URL:       repo.CloneURL.HTTPS,
			Branch:    repo.DefaultBranch,
			WithBlobs: req.WithBlobs,
			Depth:     req.Depth,
			Creds:     creds,
		},
	})
}

func (s *Service) SnapshotInfo(ctx context.Context, token, repoID, runID string) (snapshot.Info, error) {
	if _, err := s.api.Repository(ctx, token, repoID); err != nil {
		return snapshot.Info{}, err
	}
	return s.snapshots.Stat(ctx, repoID, runID)
}

func (s *Service) DeleteSnapshot(ctx context.Context, token, repoID, runID string) error {
	if _, err := s.api.Repository(ctx, token, repoID); err != nil {
		return err
	}
	return s.snapshots.Delete(ctx, repoID, runID)
}

func (s *Service) ReapSnapshots(ctx context.Context) (int, error) {
	return s.snapshots.Reap(ctx)
}

// ErrEmptyRepository — в репозитории нет коммитов, клонировать нечего.
var ErrEmptyRepository = errors.New("service: repository is empty")

// withRepo даёт fn локальную bare-копию репозитория: из снапшота runID или, если runID пуст,
// из эфемерного клона только для этого запроса. withBlobs=true — клон с содержимым файлов
// (нужен code-health/documentation). Копия удаляется сразу после fn.
func (s *Service) withRepo(ctx context.Context, token, repoID, runID string, withBlobs bool, fn func(repoDir string) error) error {
	repo, err := s.api.Repository(ctx, token, repoID)
	if err != nil {
		return err
	}
	if repo.IsEmpty {
		return ErrEmptyRepository
	}

	if runID != "" {
		ws, err := s.snapshots.Open(ctx, repo.ID, runID)
		if err != nil {
			return err
		}
		defer ws.Close()
		return fn(ws.RepoDir)
	}

	creds, err := s.gitCredentials(ctx, token)
	if err != nil {
		return err
	}
	tmp, err := os.MkdirTemp(s.workDir, "ephemeral-*")
	if err != nil {
		return err
	}
	defer os.RemoveAll(tmp)
	dir := tmp + "/repo.git"
	if err := s.git.CloneBare(ctx, dir, gitrepo.CloneOptions{URL: repo.CloneURL.HTTPS, Branch: repo.DefaultBranch, WithBlobs: withBlobs, Creds: creds}); err != nil {
		return err
	}
	return fn(dir)
}

func (s *Service) CommitActivity(ctx context.Context, token, repoID, runID string) (contract.CommitActivity, error) {
	activity := contract.CommitActivity{CommitsByDay: map[string]int{}}
	err := s.withRepo(ctx, token, repoID, runID, false, func(dir string) error {
		return s.git.WalkCommits(ctx, dir, func(c gitrepo.Commit) error {
			at := c.AuthoredAt.UTC()
			activity.TotalCount++
			activity.CommitsByDay[at.Format(time.DateOnly)]++
			if activity.FirstCommitAt == nil || at.Before(*activity.FirstCommitAt) {
				activity.FirstCommitAt = &at
			}
			if activity.LastCommitAt == nil || at.After(*activity.LastCommitAt) {
				activity.LastCommitAt = &at
			}
			return nil
		})
	})
	if errors.Is(err, ErrEmptyRepository) {
		return activity, nil
	}
	return activity, err
}

// Contributors считает коммиты по авторам из git-истории. API SourceCraft не отдаёт число коммитов,
// поэтому Login — имя автора из git, а авторы объединяются по email.
func (s *Service) Contributors(ctx context.Context, token, repoID, runID string) ([]contract.Contributor, error) {
	type agg struct {
		name  string
		count int
		bot   bool
	}
	byEmail := map[string]*agg{}
	var order []string

	err := s.withRepo(ctx, token, repoID, runID, false, func(dir string) error {
		return s.git.WalkCommits(ctx, dir, func(c gitrepo.Commit) error {
			key := strings.ToLower(c.AuthorEmail)
			if key == "" {
				key = "name:" + c.AuthorName
			}
			a, ok := byEmail[key]
			if !ok {
				a = &agg{name: c.AuthorName, bot: isBot(c.AuthorName, c.AuthorEmail)}
				byEmail[key] = a
				order = append(order, key)
			}
			a.count++
			return nil
		})
	})
	if errors.Is(err, ErrEmptyRepository) {
		return []contract.Contributor{}, nil
	}
	if err != nil {
		return nil, err
	}

	out := make([]contract.Contributor, 0, len(order))
	for _, k := range order {
		a := byEmail[k]
		out = append(out, contract.Contributor{Login: a.name, CommitsCount: a.count, IsBot: a.bot})
	}
	return out, nil
}

func (s *Service) gitCredentials(ctx context.Context, token string) (*gitrepo.Credentials, error) {
	if token == "" {
		return nil, nil
	}
	username := s.gitUsername
	if username == "" {
		u, err := s.api.CurrentUser(ctx, token)
		if err != nil {
			return nil, fmt.Errorf("resolve git username: %w", err)
		}
		username = u.Username
	}
	return &gitrepo.Credentials{Username: username, Token: token}, nil
}

func isBot(name, email string) bool {
	n, e := strings.ToLower(name), strings.ToLower(email)
	return strings.HasSuffix(n, "[bot]") || strings.HasSuffix(n, "-bot") || strings.HasSuffix(n, " bot") ||
		strings.Contains(e, "[bot]") || strings.HasPrefix(e, "bot@") ||
		strings.Contains(n, "dependabot") || strings.Contains(n, "renovate")
}

func toRepository(r sourcecraft.Repository) contract.SourceCraftRepository {
	likes := 0
	for _, rc := range r.Rating.ReactionCounts {
		likes += rc.Count.Int()
	}
	name := r.Slug
	if name == "" {
		name = r.Name
	}
	return contract.SourceCraftRepository{
		ID:             r.ID,
		Name:           name,
		FullName:       r.Organization.Slug + "/" + name,
		URL:            r.WebURL,
		Language:       r.Language.Name,
		LikesCount:     likes,
		LastActivityAt: r.LastUpdated,
		IsPrivate:      r.Visibility != "public",
		DefaultBranch:  r.DefaultBranch,
	}
}

func toPipelineStatus(s string) contract.PipelineStatus {
	switch s {
	case "success":
		return contract.PipelineSuccess
	case "failed", "timeout", "rejected":
		return contract.PipelineFailed
	case "canceled":
		return contract.PipelineCanceled
	case "skipped":
		return contract.PipelineSkipped
	default: // created, prepared, processing, awaiting_approval
		return contract.PipelineRunning
	}
}

func firstTime(ts ...*time.Time) *time.Time {
	for _, t := range ts {
		if t != nil && !t.IsZero() {
			return t
		}
	}
	return nil
}

func mapSlice[T, U any](in []T, f func(T) U) []U {
	out := make([]U, len(in))
	for i, v := range in {
		out[i] = f(v)
	}
	return out
}

// CodeHealth считает TODO/FIXME и давность самого старого маркера по git-истории.
func (s *Service) CodeHealth(ctx context.Context, token, repoID, runID string) (contract.CodeHealthReport, error) {
	report := contract.CodeHealthReport{}
	err := s.withRepo(ctx, token, repoID, runID, true, func(dir string) error {
		stats, err := s.git.MarkerStats(ctx, dir)
		if err != nil {
			return err
		}
		report.TodoCount = stats.TodoCount
		report.FixmeCount = stats.FixmeCount
		report.TotalCommentCount = stats.TodoCount + stats.FixmeCount
		if stats.HasOldest {
			age := time.Since(stats.OldestAt)
			if age < 0 {
				age = 0
			}
			formatted := contract.FormatTimeSpan(age)
			report.OldestCommentAge = &formatted
		}
		return nil
	})
	if errors.Is(err, ErrEmptyRepository) {
		return report, nil
	}
	return report, err
}

// Documentation определяет наличие README, лицензии, CONTRIBUTING, CODEOWNERS и инструкций.
//
// Инструкции ищутся по слову целиком (regexp \b, без учёта регистра) в README и
// CONTRIBUTING, чтобы «runtime», «latest» и «contest» не считались за run/test.
func (s *Service) Documentation(ctx context.Context, token, repoID, runID string) (contract.DocumentationReport, error) {
	report := contract.DocumentationReport{}
	err := s.withRepo(ctx, token, repoID, runID, true, func(dir string) error {
		files, err := s.git.ListFiles(ctx, dir)
		if err != nil {
			return err
		}
		var instructions strings.Builder
		appendContent := func(file string) {
			if content, err := s.git.ShowFile(ctx, dir, file); err == nil {
				instructions.Write(content)
				instructions.WriteByte('\n')
			}
		}
		for _, file := range files {
			name := strings.ToLower(filepath.Base(file))
			switch {
			case strings.HasPrefix(name, "readme"):
				report.HasReadme = true
				appendContent(file)
			case strings.HasPrefix(name, "license"), strings.HasPrefix(name, "licence"), name == "copying":
				report.HasLicense = true
			case strings.HasPrefix(name, "contributing"):
				report.HasContributing = true
				appendContent(file)
			case name == "codeowners":
				report.HasCodeOwners = true
			}
		}
		text := instructions.String()
		report.HasLocalRunInstructions = runInstructionsPattern.MatchString(text)
		report.HasBuildAndTestInstructions = buildAndTestPattern.MatchString(text)
		return nil
	})
	if errors.Is(err, ErrEmptyRepository) {
		return report, nil
	}
	return report, err
}

// runInstructionsPattern/buildAndTestPattern ищут маркеры инструкций по границам слов.
// У кириллических слов \b в RE2 не работает (ASCII), поэтому они матчатся как подстроки.
var (
	runInstructionsPattern = regexp.MustCompile(`(?i)\b(run|getting started|quick start|usage)\b|запуск`)
	buildAndTestPattern    = regexp.MustCompile(`(?i)\b(build|test|make)\b|сборка|тест`)
)
