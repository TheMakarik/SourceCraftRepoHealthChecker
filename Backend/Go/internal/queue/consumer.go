// Package queue обрабатывает триггер Message Queue: запускает создание снапшотов по сообщениям.
package queue

import (
	"context"
	"crypto/subtle"
	"encoding/json"
	"io"
	"log/slog"
	"net/http"

	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/service"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/snapshot"
)

const (
	maxBodyBytes = 1 << 20

	statusCreated = "created"
	statusExists  = "exists"
	statusFailed  = "failed"
)

// SnapshotCreator — минимальный порт service, нужный потребителю очереди.
type SnapshotCreator interface {
	CreateSnapshot(ctx context.Context, token string, req service.SnapshotRequest) (snapshot.Info, bool, error)
}

type Consumer struct {
	svc           SnapshotCreator
	token         string
	internalToken string
	log           *slog.Logger
}

func NewConsumer(svc SnapshotCreator, token, internalToken string, log *slog.Logger) *Consumer {
	return &Consumer{svc: svc, token: token, internalToken: internalToken, log: log}
}

// Result — итог обработки одного сообщения.
type Result struct {
	MessageID    string `json:"messageId,omitempty"`
	RepositoryID string `json:"repositoryId"`
	RunID        string `json:"runId"`
	Status       string `json:"status"`
	Created      bool   `json:"created"`
	Error        string `json:"error,omitempty"`
}

type triggerEnvelope struct {
	Messages []triggerMessage `json:"messages"`
}

type triggerMessage struct {
	Details struct {
		Message struct {
			MessageID string `json:"message_id"`
			Body      string `json:"body"`
		} `json:"message"`
	} `json:"details"`
}

type payload struct {
	RepositoryID string `json:"repositoryId"`
	RunID        string `json:"runId"`
	WithBlobs    bool   `json:"withBlobs"`
	Depth        int    `json:"depth"`
}

func (c *Consumer) ServeHTTP(w http.ResponseWriter, r *http.Request) {
	if !c.authorized(r) {
		c.writeJSON(w, http.StatusForbidden, map[string]string{"code": "forbidden", "message": "internal endpoint"})
		return
	}

	body, err := io.ReadAll(http.MaxBytesReader(w, r.Body, maxBodyBytes))
	if err != nil {
		c.writeJSON(w, http.StatusBadRequest, map[string]string{"code": "bad_request", "message": "cannot read body"})
		return
	}
	var envelope triggerEnvelope
	if err := json.Unmarshal(body, &envelope); err != nil {
		c.writeJSON(w, http.StatusBadRequest, map[string]string{"code": "bad_request", "message": "invalid trigger envelope"})
		return
	}
	if len(envelope.Messages) == 0 {
		c.writeJSON(w, http.StatusBadRequest, map[string]string{"code": "bad_request", "message": "no messages"})
		return
	}

	results := make([]Result, 0, len(envelope.Messages))
	failed := false
	for _, message := range envelope.Messages {
		result := c.process(r.Context(), message)
		if result.Status == statusFailed {
			failed = true
		}
		results = append(results, result)
	}

	status := http.StatusOK
	if failed {
		status = http.StatusBadGateway
	}
	c.writeJSON(w, status, map[string]any{"results": results})
}

func (c *Consumer) process(ctx context.Context, message triggerMessage) Result {
	result := Result{MessageID: message.Details.Message.MessageID}

	var body payload
	if err := json.Unmarshal([]byte(message.Details.Message.Body), &body); err != nil {
		result.Status = statusFailed
		result.Error = "invalid message body"
		return result
	}
	result.RepositoryID, result.RunID = body.RepositoryID, body.RunID
	if body.RepositoryID == "" || body.RunID == "" {
		result.Status = statusFailed
		result.Error = "repositoryId and runId are required"
		return result
	}
	if body.Depth < 0 {
		result.Status = statusFailed
		result.Error = "depth must be >= 0"
		return result
	}

	_, created, err := c.svc.CreateSnapshot(ctx, c.token, service.SnapshotRequest{
		RepositoryID: body.RepositoryID,
		RunID:        body.RunID,
		WithBlobs:    body.WithBlobs,
		Depth:        body.Depth,
	})
	if err != nil {
		c.log.ErrorContext(ctx, "queue snapshot failed",
			"messageId", result.MessageID, "repositoryId", body.RepositoryID, "runId", body.RunID, "err", err)
		result.Status = statusFailed
		result.Error = "snapshot creation failed"
		return result
	}

	result.Created = created
	if created {
		result.Status = statusCreated
	} else {
		result.Status = statusExists
	}
	return result
}

func (c *Consumer) authorized(r *http.Request) bool {
	if c.internalToken == "" {
		return false
	}
	got := r.Header.Get("X-Internal-Token")
	return subtle.ConstantTimeCompare([]byte(got), []byte(c.internalToken)) == 1
}

func (c *Consumer) writeJSON(w http.ResponseWriter, status int, v any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(status)
	if err := json.NewEncoder(w).Encode(v); err != nil {
		c.log.Error("encode response", "err", err)
	}
}
