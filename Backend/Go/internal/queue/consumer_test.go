package queue

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"io"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"testing"

	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/service"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/snapshot"
)

type fakeService struct {
	calls   []service.SnapshotRequest
	created bool
	err     error
}

func (f *fakeService) CreateSnapshot(_ context.Context, _ string, req service.SnapshotRequest) (snapshot.Info, bool, error) {
	f.calls = append(f.calls, req)
	if f.err != nil {
		return snapshot.Info{}, false, f.err
	}
	return snapshot.Info{RepositoryID: req.RepositoryID, RunID: req.RunID}, f.created, nil
}

func newTestConsumer(svc SnapshotCreator) *Consumer {
	return NewConsumer(svc, "service-pat", "internal-secret", slog.New(slog.NewTextHandler(io.Discard, nil)))
}

func triggerBody(t *testing.T, messages ...string) []byte {
	t.Helper()
	msgs := make([]map[string]any, 0, len(messages))
	for i, body := range messages {
		msgs = append(msgs, map[string]any{
			"details": map[string]any{
				"message": map[string]any{
					"message_id": "m" + string(rune('1'+i)),
					"body":       body,
				},
			},
		})
	}
	raw, err := json.Marshal(map[string]any{"messages": msgs})
	if err != nil {
		t.Fatal(err)
	}
	return raw
}

func doTrigger(t *testing.T, consumer *Consumer, body []byte, token string) (int, map[string]any) {
	t.Helper()
	req := httptest.NewRequest(http.MethodPost, "/internal/queue/messages", bytes.NewReader(body))
	if token != "" {
		req.Header.Set("X-Internal-Token", token)
	}
	rec := httptest.NewRecorder()
	consumer.ServeHTTP(rec, req)

	var decoded map[string]any
	_ = json.Unmarshal(rec.Body.Bytes(), &decoded)
	return rec.Code, decoded
}

func TestConsumerRequiresInternalToken(t *testing.T) {
	rec := httptest.NewRecorder()
	req := httptest.NewRequest(http.MethodPost, "/internal/queue/messages", bytes.NewReader(triggerBody(t, `{"repositoryId":"r1","runId":"run-1"}`)))
	newTestConsumer(&fakeService{}).ServeHTTP(rec, req)
	if rec.Code != http.StatusForbidden {
		t.Fatalf("code = %d, want 403", rec.Code)
	}
}

func TestConsumerProcessesMessages(t *testing.T) {
	fake := &fakeService{created: true}
	body := triggerBody(t,
		`{"repositoryId":"r1","runId":"run-1"}`,
		`{"repositoryId":"r2","runId":"run-2","withBlobs":true,"depth":10}`,
	)
	code, decoded := doTrigger(t, newTestConsumer(fake), body, "internal-secret")

	if code != http.StatusOK {
		t.Fatalf("code = %d, body = %v", code, decoded)
	}
	results, _ := decoded["results"].([]any)
	if len(results) != 2 {
		t.Fatalf("results = %v", decoded["results"])
	}
	first := results[0].(map[string]any)
	if first["status"] != statusCreated || first["created"] != true || first["repositoryId"] != "r1" {
		t.Fatalf("first = %v", first)
	}
	second := results[1].(map[string]any)
	if second["status"] != statusCreated || second["repositoryId"] != "r2" {
		t.Fatalf("second = %v", second)
	}

	if len(fake.calls) != 2 {
		t.Fatalf("service calls = %d, want 2", len(fake.calls))
	}
	if fake.calls[1].RepositoryID != "r2" || !fake.calls[1].WithBlobs || fake.calls[1].Depth != 10 {
		t.Fatalf("second call = %+v", fake.calls[1])
	}
}

func TestConsumerReportsExistingSnapshot(t *testing.T) {
	fake := &fakeService{created: false}
	code, decoded := doTrigger(t, newTestConsumer(fake), triggerBody(t, `{"repositoryId":"r1","runId":"run-1"}`), "internal-secret")
	if code != http.StatusOK {
		t.Fatalf("code = %d", code)
	}
	results := decoded["results"].([]any)
	if results[0].(map[string]any)["status"] != statusExists {
		t.Fatalf("result = %v", results[0])
	}
}

func TestConsumerFailsOnInvalidMessageBody(t *testing.T) {
	fake := &fakeService{created: true}
	body := triggerBody(t, `not json`, `{"repositoryId":"r1","runId":"run-1"}`)
	code, decoded := doTrigger(t, newTestConsumer(fake), body, "internal-secret")

	if code != http.StatusBadGateway {
		t.Fatalf("code = %d, body = %v", code, decoded)
	}
	results := decoded["results"].([]any)
	if results[0].(map[string]any)["status"] != statusFailed || results[1].(map[string]any)["status"] != statusCreated {
		t.Fatalf("results = %v", results)
	}
}

func TestConsumerFailsWhenServiceErrors(t *testing.T) {
	fake := &fakeService{err: errors.New("clone failed")}
	code, decoded := doTrigger(t, newTestConsumer(fake), triggerBody(t, `{"repositoryId":"r1","runId":"run-1"}`), "internal-secret")
	if code != http.StatusBadGateway {
		t.Fatalf("code = %d", code)
	}
	result := decoded["results"].([]any)[0].(map[string]any)
	if result["status"] != statusFailed || result["error"] == nil {
		t.Fatalf("result = %v", result)
	}
}

func TestConsumerRejectsInvalidEnvelope(t *testing.T) {
	code, _ := doTrigger(t, newTestConsumer(&fakeService{}), []byte(`{`), "internal-secret")
	if code != http.StatusBadRequest {
		t.Fatalf("code = %d, want 400", code)
	}
	code, _ = doTrigger(t, newTestConsumer(&fakeService{}), []byte(`{"messages":[]}`), "internal-secret")
	if code != http.StatusBadRequest {
		t.Fatalf("empty messages: code = %d, want 400", code)
	}
}

func TestConsumerRequiresRepositoryAndRunID(t *testing.T) {
	code, decoded := doTrigger(t, newTestConsumer(&fakeService{}), triggerBody(t, `{"repositoryId":"r1"}`), "internal-secret")
	if code != http.StatusBadGateway {
		t.Fatalf("code = %d", code)
	}
	result := decoded["results"].([]any)[0].(map[string]any)
	if result["status"] != statusFailed {
		t.Fatalf("result = %v", result)
	}
}
