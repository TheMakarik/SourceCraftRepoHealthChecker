package serverless

import (
	"crypto/subtle"
	"encoding/json"
	"log/slog"
	"net/http"

	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/service"
)

// Timer — обработчик таймер-триггера: периодическая очистка просроченных снапшотов.
type Timer struct {
	svc           *service.Service
	internalToken string
	log           *slog.Logger
}

func NewTimer(svc *service.Service, internalToken string, log *slog.Logger) *Timer {
	return &Timer{svc: svc, internalToken: internalToken, log: log}
}

func (t *Timer) Reap(w http.ResponseWriter, r *http.Request) {
	if !authorizedInternal(r, t.internalToken) {
		writeJSON(w, http.StatusForbidden, map[string]string{"code": "forbidden", "message": "internal endpoint"})
		return
	}
	deleted, err := t.svc.ReapSnapshots(r.Context())
	if err != nil {
		t.log.ErrorContext(r.Context(), "reap snapshots", "deleted", deleted, "err", err)
		writeJSON(w, http.StatusBadGateway, map[string]string{"code": "storage_error", "message": "failed to reap some snapshots"})
		return
	}
	writeJSON(w, http.StatusOK, map[string]int{"deleted": deleted})
}

func authorizedInternal(r *http.Request, token string) bool {
	if token == "" {
		return false
	}
	got := r.Header.Get("X-Internal-Token")
	return subtle.ConstantTimeCompare([]byte(got), []byte(token)) == 1
}

func writeJSON(w http.ResponseWriter, status int, v any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(status)
	_ = json.NewEncoder(w).Encode(v)
}
