// Package httpapi — REST-обёртка над service для C#-бэкенда (раздел 2 и 6 ТЗ Go).
package httpapi

import (
	"context"
	"crypto/rand"
	"crypto/subtle"
	"encoding/hex"
	"encoding/json"
	"errors"
	"log/slog"
	"net/http"
	"reflect"
	"strconv"
	"strings"
	"time"

	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/contract"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/service"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/snapshot"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/sourcecraft"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/yandexid"
)

type Server struct {
	svc           *service.Service
	yandexID      *yandexid.Client
	serviceToken  string
	internalToken string
	log           *slog.Logger
}

type Options struct {
	// ServiceToken используется, если запрос пришёл без Authorization (публичные данные).
	ServiceToken string
	// InternalToken защищает служебные эндпоинты (таймер-триггер).
	InternalToken string
	// YandexID — вход через Яндекс ID; nil или без кредов — /auth/url и /auth/token отвечают 501.
	YandexID *yandexid.Client
}

func New(svc *service.Service, opts Options, log *slog.Logger) http.Handler {
	s := &Server{svc: svc, yandexID: opts.YandexID, serviceToken: opts.ServiceToken, internalToken: opts.InternalToken, log: log}
	mux := http.NewServeMux()

	mux.HandleFunc("GET /healthz", func(w http.ResponseWriter, _ *http.Request) { w.WriteHeader(http.StatusNoContent) })

	mux.HandleFunc("POST /auth/url", s.yandexIDOnly(s.authURL))
	mux.HandleFunc("POST /auth/token", s.yandexIDOnly(s.authToken))
	mux.HandleFunc("GET /auth/me", s.userOnly(s.me))
	mux.HandleFunc("GET /auth/repositories", s.userOnly(s.myRepositories))

	mux.HandleFunc("GET /repositories", s.catalog)
	mux.HandleFunc("GET /repositories/{id}", s.repository)
	mux.HandleFunc("GET /repositories/{id}/activity/commits", s.commits)
	mux.HandleFunc("GET /repositories/{id}/activity/contributors", s.contributors)
	mux.HandleFunc("GET /repositories/{id}/activity/releases", s.releases)
	mux.HandleFunc("GET /repositories/{id}/issues", s.issues)
	mux.HandleFunc("GET /repositories/{id}/merge-requests", s.mergeRequests)
	mux.HandleFunc("GET /repositories/{id}/pipelines", s.pipelines)
	mux.HandleFunc("GET /repositories/{id}/code-health", s.codeHealth)
	mux.HandleFunc("GET /repositories/{id}/documentation", s.documentation)
	mux.HandleFunc("GET /repositories/{id}/security/findings", s.securityFindings)

	mux.HandleFunc("PUT /repositories/{id}/snapshots/{runId}", s.createSnapshot)
	mux.HandleFunc("GET /repositories/{id}/snapshots/{runId}", s.getSnapshot)
	mux.HandleFunc("DELETE /repositories/{id}/snapshots/{runId}", s.deleteSnapshot)

	mux.HandleFunc("POST /internal/snapshots/reap", s.internalOnly(s.reapSnapshots))

	return s.middleware(mux)
}

type ctxKey struct{}

func requestID(ctx context.Context) string {
	id, _ := ctx.Value(ctxKey{}).(string)
	return id
}

func (s *Server) middleware(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		id := r.Header.Get("X-Request-Id")
		if id == "" || len(id) > 128 {
			var b [8]byte
			_, _ = rand.Read(b[:])
			id = hex.EncodeToString(b[:])
		}
		w.Header().Set("X-Request-Id", id)
		r = r.WithContext(context.WithValue(r.Context(), ctxKey{}, id))

		start := time.Now()
		rec := &statusRecorder{ResponseWriter: w, status: http.StatusOK}
		next.ServeHTTP(rec, r)
		// Логируем только маршрут и статус: без токенов, query и тел ответов.
		s.log.InfoContext(r.Context(), "http request",
			"requestId", id, "method", r.Method, "route", r.Pattern,
			"status", rec.status, "durationMs", time.Since(start).Milliseconds())
	})
}

type statusRecorder struct {
	http.ResponseWriter
	status int
}

func (r *statusRecorder) WriteHeader(code int) {
	r.status = code
	r.ResponseWriter.WriteHeader(code)
}

// token — токен пользователя из Authorization, иначе сервисный.
func (s *Server) token(r *http.Request) string {
	if t := bearer(r); t != "" {
		return t
	}
	return s.serviceToken
}

func bearer(r *http.Request) string {
	h := r.Header.Get("Authorization")
	if len(h) > 7 && strings.EqualFold(h[:7], "bearer ") {
		return strings.TrimSpace(h[7:])
	}
	return ""
}

func (s *Server) userOnly(h http.HandlerFunc) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		if bearer(r) == "" {
			s.writeError(w, r, http.StatusUnauthorized, "unauthorized", "user access token required")
			return
		}
		h(w, r)
	}
}

func (s *Server) internalOnly(h http.HandlerFunc) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		got := r.Header.Get("X-Internal-Token")
		if s.internalToken == "" || subtle.ConstantTimeCompare([]byte(got), []byte(s.internalToken)) != 1 {
			s.writeError(w, r, http.StatusForbidden, "forbidden", "internal endpoint")
			return
		}
		h(w, r)
	}
}

func (s *Server) yandexIDOnly(h http.HandlerFunc) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		if s.yandexID == nil || !s.yandexID.Configured() {
			s.writeError(w, r, http.StatusNotImplemented, "not_configured", "Yandex ID OAuth app is not configured (YANDEX_CLIENT_ID, YANDEX_CLIENT_SECRET)")
			return
		}
		h(w, r)
	}
}

// maxStateLen ограничивает state: он целиком уходит в URL авторизации.
const maxStateLen = 512

// authURL — ссылка на вход через Яндекс ID. state генерирует и после callback проверяет C# (защита от CSRF).
func (s *Server) authURL(w http.ResponseWriter, r *http.Request) {
	var body struct {
		State string `json:"state"`
	}
	if !s.decodeBody(w, r, &body) {
		return
	}
	if body.State == "" || len(body.State) > maxStateLen {
		s.writeError(w, r, http.StatusBadRequest, "bad_request", "state is required (up to 512 chars)")
		return
	}
	s.writeJSON(w, http.StatusOK, s.envelope(r, map[string]string{"url": s.yandexID.AuthorizationURL(body.State)}))
}

// authToken обменивает код подтверждения Яндекс ID на профиль пользователя.
// Токен Яндекса наружу не отдаётся: API SourceCraft его не принимает, для приватных данных нужен PAT.
func (s *Server) authToken(w http.ResponseWriter, r *http.Request) {
	var body struct {
		Code  string `json:"code"`
		State string `json:"state"`
	}
	if !s.decodeBody(w, r, &body) {
		return
	}
	if body.Code == "" || body.State == "" || len(body.State) > maxStateLen {
		s.writeError(w, r, http.StatusBadRequest, "bad_request", "code and state are required")
		return
	}

	u, err := s.yandexID.Authenticate(r.Context(), body.Code)
	switch {
	case err == nil:
		var email *string
		if u.DefaultEmail != "" {
			email = &u.DefaultEmail
		}
		s.writeJSON(w, http.StatusOK, s.envelope(r, contract.SourceCraftUser{ID: u.ID, Login: u.Login, DisplayName: u.DisplayName, Email: email}))
	case errors.Is(err, yandexid.ErrInvalidGrant):
		s.writeError(w, r, http.StatusBadRequest, "invalid_grant", "authorization code is invalid, expired or already used")
	case errors.Is(err, context.DeadlineExceeded):
		s.writeError(w, r, http.StatusGatewayTimeout, "timeout", "Yandex ID timed out")
	case errors.Is(err, context.Canceled):
	case errors.Is(err, yandexid.ErrUnavailable):
		s.log.WarnContext(r.Context(), "yandex id unavailable", "requestId", requestID(r.Context()), "err", err)
		s.writeError(w, r, http.StatusServiceUnavailable, "yandex_id_unavailable", "Yandex ID is temporarily unavailable")
	default:
		s.log.ErrorContext(r.Context(), "yandex id login failed", "requestId", requestID(r.Context()), "err", err)
		s.writeError(w, r, http.StatusBadGateway, "yandex_id_error", "Yandex ID login failed")
	}
}

func (s *Server) decodeBody(w http.ResponseWriter, r *http.Request, v any) bool {
	if err := json.NewDecoder(http.MaxBytesReader(w, r.Body, 8<<10)).Decode(v); err != nil {
		s.writeError(w, r, http.StatusBadRequest, "bad_request", "invalid JSON body")
		return false
	}
	return true
}

func (s *Server) me(w http.ResponseWriter, r *http.Request) {
	u, err := s.svc.CurrentUser(r.Context(), bearer(r))
	s.respond(w, r, u, err)
}

func (s *Server) myRepositories(w http.ResponseWriter, r *http.Request) {
	repos, err := s.svc.MyRepositories(r.Context(), bearer(r))
	s.respond(w, r, repos, err)
}

func (s *Server) catalog(w http.ResponseWriter, r *http.Request) {
	q := r.URL.Query()
	pageSize, _ := strconv.Atoi(q.Get("pageSize"))
	repos, next, err := s.svc.Catalog(r.Context(), s.token(r), q.Get("pageToken"), pageSize, q.Get("sortBy"))
	if err != nil {
		s.respond(w, r, nil, err)
		return
	}
	env := s.envelope(r, repos)
	env.NextPageToken = next
	s.writeJSON(w, http.StatusOK, env)
}

func (s *Server) repository(w http.ResponseWriter, r *http.Request) {
	repo, err := s.svc.Repository(r.Context(), s.token(r), r.PathValue("id"))
	s.respond(w, r, repo, err)
}

func (s *Server) commits(w http.ResponseWriter, r *http.Request) {
	a, err := s.svc.CommitActivity(r.Context(), s.token(r), r.PathValue("id"), r.URL.Query().Get("runId"))
	if err == nil && a.TotalCount == 0 {
		s.writeJSON(w, http.StatusOK, s.noData(r, a, "repository has no commits"))
		return
	}
	s.respond(w, r, a, err)
}

func (s *Server) contributors(w http.ResponseWriter, r *http.Request) {
	c, err := s.svc.Contributors(r.Context(), s.token(r), r.PathValue("id"), r.URL.Query().Get("runId"))
	s.respond(w, r, c, err)
}

func (s *Server) releases(w http.ResponseWriter, r *http.Request) {
	rel, err := s.svc.Releases(r.Context(), s.token(r), r.PathValue("id"))
	s.respond(w, r, rel, err)
}

func (s *Server) issues(w http.ResponseWriter, r *http.Request) {
	is, err := s.svc.Issues(r.Context(), s.token(r), r.PathValue("id"))
	s.respond(w, r, is, err)
}

func (s *Server) mergeRequests(w http.ResponseWriter, r *http.Request) {
	mr, err := s.svc.MergeRequests(r.Context(), s.token(r), r.PathValue("id"))
	s.respond(w, r, mr, err)
}

func (s *Server) pipelines(w http.ResponseWriter, r *http.Request) {
	p, err := s.svc.Pipelines(r.Context(), s.token(r), r.PathValue("id"))
	s.respond(w, r, p, err)
}

func (s *Server) codeHealth(w http.ResponseWriter, r *http.Request) {
	report, err := s.svc.CodeHealth(r.Context(), s.token(r), r.PathValue("id"), r.URL.Query().Get("runId"))
	s.respond(w, r, report, err)
}

func (s *Server) documentation(w http.ResponseWriter, r *http.Request) {
	report, err := s.svc.Documentation(r.Context(), s.token(r), r.PathValue("id"), r.URL.Query().Get("runId"))
	s.respond(w, r, report, err)
}

// securityFindings: публичный REST API SourceCraft пока не отдаёт результаты AppSec (SAST/SCA/secrets),
// а имитировать сканирование ТЗ запрещает — поэтому честный Unavailable.
func (s *Server) securityFindings(w http.ResponseWriter, r *http.Request) {
	if _, err := s.svc.Repository(r.Context(), s.token(r), r.PathValue("id")); err != nil {
		s.respond(w, r, nil, err)
		return
	}
	env := s.envelope(r, []any{})
	env.Status = contract.Unavailable
	env.Reason = "SourceCraft public API does not expose AppSec findings yet"
	s.writeJSON(w, http.StatusOK, env)
}

type snapshotBody struct {
	WithBlobs bool `json:"withBlobs"`
	Depth     int  `json:"depth"`
}

func (s *Server) createSnapshot(w http.ResponseWriter, r *http.Request) {
	var body snapshotBody
	if r.ContentLength != 0 {
		if err := json.NewDecoder(http.MaxBytesReader(w, r.Body, 4<<10)).Decode(&body); err != nil {
			s.writeError(w, r, http.StatusBadRequest, "bad_request", "invalid JSON body")
			return
		}
	}
	if body.Depth < 0 {
		s.writeError(w, r, http.StatusBadRequest, "bad_request", "depth must be >= 0")
		return
	}
	info, created, err := s.svc.CreateSnapshot(r.Context(), s.token(r), service.SnapshotRequest{
		RepositoryID: r.PathValue("id"),
		RunID:        r.PathValue("runId"),
		WithBlobs:    body.WithBlobs,
		Depth:        body.Depth,
	})
	if err != nil {
		s.respond(w, r, nil, err)
		return
	}
	status := http.StatusOK
	if created {
		status = http.StatusCreated
	}
	s.writeJSON(w, status, s.envelope(r, info))
}

func (s *Server) getSnapshot(w http.ResponseWriter, r *http.Request) {
	info, err := s.svc.SnapshotInfo(r.Context(), s.token(r), r.PathValue("id"), r.PathValue("runId"))
	s.respond(w, r, info, err)
}

func (s *Server) deleteSnapshot(w http.ResponseWriter, r *http.Request) {
	if err := s.svc.DeleteSnapshot(r.Context(), s.token(r), r.PathValue("id"), r.PathValue("runId")); err != nil {
		s.respond(w, r, nil, err)
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

func (s *Server) reapSnapshots(w http.ResponseWriter, r *http.Request) {
	n, err := s.svc.ReapSnapshots(r.Context())
	if err != nil {
		s.log.ErrorContext(r.Context(), "reap snapshots", "requestId", requestID(r.Context()), "deleted", n, "err", err)
		s.writeError(w, r, http.StatusBadGateway, "storage_error", "failed to reap some snapshots")
		return
	}
	s.writeJSON(w, http.StatusOK, s.envelope(r, map[string]int{"deleted": n}))
}

func (s *Server) envelope(r *http.Request, data any) contract.Envelope {
	return contract.Envelope{
		RequestID:   requestID(r.Context()),
		CollectedAt: time.Now().UTC(),
		Status:      contract.Available,
		Data:        data,
	}
}

func (s *Server) noData(r *http.Request, data any, reason string) contract.Envelope {
	env := s.envelope(r, data)
	env.Status, env.Reason = contract.NoData, reason
	return env
}

// respond отдаёт данные категории. Ошибки доступа и адресации — HTTP-кодами (раздел 6 ТЗ),
// а сбой внешнего источника — статусом Unavailable, чтобы не ронять весь снапшот.
func (s *Server) respond(w http.ResponseWriter, r *http.Request, data any, err error) {
	if err == nil {
		if isEmpty(data) {
			s.writeJSON(w, http.StatusOK, s.noData(r, data, "source returned no items"))
			return
		}
		s.writeJSON(w, http.StatusOK, s.envelope(r, data))
		return
	}

	switch {
	case sourcecraft.IsUnauthorized(err):
		s.writeError(w, r, http.StatusUnauthorized, "unauthorized", "SourceCraft rejected the access token")
	case sourcecraft.IsForbidden(err):
		s.writeError(w, r, http.StatusForbidden, "forbidden", "no access to the repository")
	case sourcecraft.IsNotFound(err):
		s.writeError(w, r, http.StatusNotFound, "not_found", "repository not found")
	case errors.Is(err, snapshot.ErrNotFound):
		s.writeError(w, r, http.StatusNotFound, "snapshot_not_found", "snapshot not found or expired")
	case errors.Is(err, snapshot.ErrInvalidID):
		s.writeError(w, r, http.StatusBadRequest, "bad_request", err.Error())
	case errors.Is(err, service.ErrEmptyRepository):
		s.writeJSON(w, http.StatusOK, s.noData(r, data, "repository is empty"))
	case errors.Is(err, context.DeadlineExceeded):
		s.writeError(w, r, http.StatusGatewayTimeout, "timeout", "data collection timed out")
	case errors.Is(err, context.Canceled):
		// Клиент ушёл — отвечать некому.
	case errors.Is(err, snapshot.ErrTooLarge):
		s.unavailable(w, r, err, "repository exceeds analysis size limit")
	case errors.Is(err, sourcecraft.ErrUnavailable):
		s.unavailable(w, r, err, "SourceCraft is temporarily unavailable")
	default:
		s.unavailable(w, r, err, "data source failed")
	}
}

func (s *Server) unavailable(w http.ResponseWriter, r *http.Request, err error, reason string) {
	s.log.WarnContext(r.Context(), "category unavailable", "requestId", requestID(r.Context()), "route", r.Pattern, "err", err)
	env := s.envelope(r, nil)
	env.Status, env.Reason = contract.Unavailable, reason
	s.writeJSON(w, http.StatusOK, env)
}

func (s *Server) writeError(w http.ResponseWriter, r *http.Request, status int, code, msg string) {
	s.writeJSON(w, status, contract.Error{RequestID: requestID(r.Context()), Code: code, Message: msg})
}

func (s *Server) writeJSON(w http.ResponseWriter, status int, v any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(status)
	if err := json.NewEncoder(w).Encode(v); err != nil {
		s.log.Error("encode response", "err", err)
	}
}

func isEmpty(v any) bool {
	rv := reflect.ValueOf(v)
	return rv.Kind() == reflect.Slice && rv.Len() == 0
}
