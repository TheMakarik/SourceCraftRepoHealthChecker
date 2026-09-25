// Package snapshot — краткоживущее хранение рабочей копии репозитория в S3-совместимом хранилище.
//
// Жизненный цикл (раздел 5 ТЗ Go):
//  1. Create: компактный bare-клон на эфемерный диск → tar.gz потоком в S3 → локальная копия удаляется.
//  2. Open: скачивание снапшота во временный каталог на время одного прогона; Workspace.Close удаляет его.
//  3. Delete: явное удаление после анализа (успех или ошибка).
//  4. Reap + lifecycle-политика бакета: удаление всего, что пережило TTL.
package snapshot

import (
	"context"
	"errors"
	"fmt"
	"io"
	"log/slog"
	"os"
	"path"
	"regexp"
	"strconv"
	"time"

	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/gitrepo"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/objectstore"
)

const archiveName = "repo.tar.gz"

// Ключи метаданных объекта снапшота.
const (
	metaExpiresAt = "expires-at"
	metaCreatedAt = "created-at"
	metaBranch    = "branch"
	metaWithBlobs = "with-blobs"
	metaRepoBytes = "repo-bytes"
)

var (
	ErrNotFound  = errors.New("snapshot: not found")
	ErrInvalidID = errors.New("snapshot: invalid repository or run id")
	ErrTooLarge  = gitrepo.ErrRepoTooLarge
)

var idPattern = regexp.MustCompile(`^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$`)

type Options struct {
	Prefix       string
	TTL          time.Duration
	WorkDir      string
	MaxRepoBytes int64
	CloneTimeout time.Duration
}

type Store struct {
	objects objectstore.Store
	git     gitrepo.Git
	opts    Options
	log     *slog.Logger
	now     func() time.Time
}

func NewStore(objects objectstore.Store, git gitrepo.Git, opts Options, log *slog.Logger) *Store {
	if opts.TTL <= 0 {
		opts.TTL = 3 * time.Hour
	}
	return &Store{objects: objects, git: git, opts: opts, log: log, now: time.Now}
}

// Info описывает снапшот. Содержимое кода сюда не попадает.
type Info struct {
	RepositoryID string    `json:"repositoryId"`
	RunID        string    `json:"runId"`
	Branch       string    `json:"branch"`
	WithBlobs    bool      `json:"withBlobs"`
	RepoBytes    int64     `json:"repoBytes"`
	ArchiveBytes int64     `json:"archiveBytes"`
	CreatedAt    time.Time `json:"createdAt"`
	ExpiresAt    time.Time `json:"expiresAt"`
}

type CreateRequest struct {
	RepositoryID string
	RunID        string
	Clone        gitrepo.CloneOptions
}

// Create клонирует репозиторий и кладёт снапшот в хранилище. Идемпотентен по (repositoryId, runId):
// если живой снапшот уже есть, возвращает его и created=false.
func (s *Store) Create(ctx context.Context, req CreateRequest) (info Info, created bool, err error) {
	key, err := s.key(req.RepositoryID, req.RunID)
	if err != nil {
		return Info{}, false, err
	}

	if existing, err := s.stat(ctx, req.RepositoryID, req.RunID, key); err == nil {
		return existing, false, nil
	} else if !errors.Is(err, ErrNotFound) {
		return Info{}, false, err
	}

	if s.opts.CloneTimeout > 0 {
		var cancel context.CancelFunc
		ctx, cancel = context.WithTimeout(ctx, s.opts.CloneTimeout)
		defer cancel()
	}

	tmp, err := os.MkdirTemp(s.opts.WorkDir, "clone-*")
	if err != nil {
		return Info{}, false, err
	}
	defer s.removeAll(tmp)

	repoDir := tmp + "/repo.git"
	if err := s.git.CloneBare(ctx, repoDir, req.Clone); err != nil {
		return Info{}, false, err
	}
	repoBytes, err := gitrepo.DirSize(repoDir)
	if err != nil {
		return Info{}, false, err
	}
	if s.opts.MaxRepoBytes > 0 && repoBytes > s.opts.MaxRepoBytes {
		return Info{}, false, fmt.Errorf("%w: %d bytes > %d", ErrTooLarge, repoBytes, s.opts.MaxRepoBytes)
	}

	now := s.now().UTC()
	info = Info{
		RepositoryID: req.RepositoryID,
		RunID:        req.RunID,
		Branch:       req.Clone.Branch,
		WithBlobs:    req.Clone.WithBlobs,
		RepoBytes:    repoBytes,
		CreatedAt:    now,
		ExpiresAt:    now.Add(s.opts.TTL),
	}

	pr, pw := io.Pipe()
	packed := make(chan struct{})
	go func() {
		defer close(packed)
		pw.CloseWithError(writeArchive(pw, repoDir))
	}()
	obj, err := s.objects.Put(ctx, key, pr, toMetadata(info))
	_ = pr.CloseWithError(errors.New("upload finished"))
	<-packed
	if err != nil {
		// Недокачанный multipart подчистит lifecycle AbortIncompleteMultipartUpload.
		_ = s.objects.Delete(context.WithoutCancel(ctx), key)
		return Info{}, false, err
	}
	info.ArchiveBytes = obj.Size

	s.log.InfoContext(ctx, "snapshot created",
		"repositoryId", info.RepositoryID, "runId", info.RunID,
		"repoBytes", info.RepoBytes, "archiveBytes", info.ArchiveBytes)
	return info, true, nil
}

func (s *Store) Stat(ctx context.Context, repositoryID, runID string) (Info, error) {
	key, err := s.key(repositoryID, runID)
	if err != nil {
		return Info{}, err
	}
	return s.stat(ctx, repositoryID, runID, key)
}

func (s *Store) stat(ctx context.Context, repositoryID, runID, key string) (Info, error) {
	obj, err := s.objects.Stat(ctx, key)
	if errors.Is(err, objectstore.ErrNotFound) {
		return Info{}, ErrNotFound
	}
	if err != nil {
		return Info{}, err
	}
	info := fromObject(repositoryID, runID, obj)
	if s.expired(info) {
		return Info{}, ErrNotFound
	}
	return info, nil
}

// Workspace — локальная распакованная копия снапшота на время одного прогона.
type Workspace struct {
	Info    Info
	RepoDir string

	root  string
	store *Store
}

// Close удаляет локальную копию. Вызывать всегда через defer.
func (w *Workspace) Close() error {
	w.store.removeAll(w.root)
	return nil
}

// Open скачивает снапшот во временный каталог.
func (s *Store) Open(ctx context.Context, repositoryID, runID string) (*Workspace, error) {
	key, err := s.key(repositoryID, runID)
	if err != nil {
		return nil, err
	}
	body, obj, err := s.objects.Get(ctx, key)
	if errors.Is(err, objectstore.ErrNotFound) {
		return nil, ErrNotFound
	}
	if err != nil {
		return nil, err
	}
	defer body.Close()

	info := fromObject(repositoryID, runID, obj)
	if s.expired(info) {
		return nil, ErrNotFound
	}

	root, err := os.MkdirTemp(s.opts.WorkDir, "ws-*")
	if err != nil {
		return nil, err
	}
	repoDir := root + "/repo.git"
	if err := os.Mkdir(repoDir, 0o755); err != nil {
		s.removeAll(root)
		return nil, err
	}
	if err := extractArchive(body, repoDir, s.opts.MaxRepoBytes); err != nil {
		s.removeAll(root)
		if errors.Is(err, errArchiveTooLarge) {
			return nil, ErrTooLarge
		}
		return nil, err
	}
	return &Workspace{Info: info, RepoDir: repoDir, root: root, store: s}, nil
}

// Delete удаляет снапшот. Отсутствие снапшота — не ошибка (идемпотентно).
func (s *Store) Delete(ctx context.Context, repositoryID, runID string) error {
	key, err := s.key(repositoryID, runID)
	if err != nil {
		return err
	}
	err = s.objects.Delete(ctx, key)
	if errors.Is(err, objectstore.ErrNotFound) {
		return nil
	}
	if err == nil {
		s.log.InfoContext(ctx, "snapshot deleted", "repositoryId", repositoryID, "runId", runID)
	}
	return err
}

// Reap удаляет все снапшоты с истёкшим TTL. Вызывается по таймеру.
func (s *Store) Reap(ctx context.Context) (deleted int, err error) {
	var errs []error
	for obj, err := range s.objects.List(ctx, s.opts.Prefix+"/") {
		if err != nil {
			return deleted, err
		}
		info := fromObject("", "", obj)
		// Объект без метаданных (например, чужой) удаляем по времени изменения.
		if info.ExpiresAt.IsZero() {
			info.ExpiresAt = obj.LastModified.Add(s.opts.TTL)
		}
		if !s.expired(info) {
			continue
		}
		if err := s.objects.Delete(ctx, obj.Key); err != nil && !errors.Is(err, objectstore.ErrNotFound) {
			errs = append(errs, err)
			continue
		}
		deleted++
	}
	return deleted, errors.Join(errs...)
}

func (s *Store) key(repositoryID, runID string) (string, error) {
	if !idPattern.MatchString(repositoryID) || !idPattern.MatchString(runID) {
		return "", ErrInvalidID
	}
	return path.Join(s.opts.Prefix, repositoryID, runID, archiveName), nil
}

func (s *Store) expired(info Info) bool {
	return !info.ExpiresAt.IsZero() && !s.now().Before(info.ExpiresAt)
}

func (s *Store) removeAll(dir string) {
	if err := os.RemoveAll(dir); err != nil {
		s.log.Error("failed to remove local repository copy", "dir", dir, "err", err)
	}
}

func toMetadata(i Info) map[string]string {
	return map[string]string{
		metaExpiresAt: i.ExpiresAt.Format(time.RFC3339),
		metaCreatedAt: i.CreatedAt.Format(time.RFC3339),
		metaBranch:    i.Branch,
		metaWithBlobs: strconv.FormatBool(i.WithBlobs),
		metaRepoBytes: strconv.FormatInt(i.RepoBytes, 10),
	}
}

func fromObject(repositoryID, runID string, o objectstore.ObjectInfo) Info {
	m := o.Metadata
	info := Info{
		RepositoryID: repositoryID,
		RunID:        runID,
		Branch:       m[metaBranch],
		ArchiveBytes: o.Size,
	}
	info.WithBlobs, _ = strconv.ParseBool(m[metaWithBlobs])
	info.RepoBytes, _ = strconv.ParseInt(m[metaRepoBytes], 10, 64)
	info.CreatedAt, _ = time.Parse(time.RFC3339, m[metaCreatedAt])
	info.ExpiresAt, _ = time.Parse(time.RFC3339, m[metaExpiresAt])
	return info
}
