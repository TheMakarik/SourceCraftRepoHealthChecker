package serverless

import (
	"encoding/json"
	"io"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
	"time"
)

var serverlessEnvKeys = []string{
	"HTTP_ADDR", "INTERNAL_TOKEN", "SNAPSHOT_REAP_INTERVAL",
	"SOURCECRAFT_API_URL", "SOURCECRAFT_TOKEN", "SOURCECRAFT_GIT_USERNAME",
	"SOURCECRAFT_TIMEOUT", "SOURCECRAFT_MAX_RETRIES", "SOURCECRAFT_RPS",
	"SERVICECRAFT_MAX_ITEMS", "SERVICECRAFT_MAX_RESPONSE_LOOKUPS", "SERVICECRAFT_CONCURRENCY",
	"YANDEX_CLIENT_ID", "YANDEX_CLIENT_SECRET", "YANDEX_REDIRECT_URI", "YANDEX_SCOPE",
	"S3_ENDPOINT", "S3_REGION", "S3_BUCKET", "S3_ACCESS_KEY", "S3_SECRET_KEY",
	"S3_USE_SSL", "S3_SSE", "S3_PREFIX", "SNAPSHOT_TTL", "S3_LIFECYCLE_DAYS",
	"GIT_BINARY", "GIT_WORKDIR", "GIT_CLONE_TIMEOUT", "GIT_MAX_REPO_MB",
}

const emptyListResultXML = `<?xml version="1.0" encoding="UTF-8"?>` +
	`<ListBucketResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/">` +
	`<Name>test-bucket</Name><IsTruncated>false</IsTruncated></ListBucketResult>`

// fakeS3 отвечает на HEAD/GET так, чтобы storage-инициализация не ходила в сеть.
func fakeS3(t *testing.T) string {
	t.Helper()
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.Method == http.MethodGet {
			w.Header().Set("Content-Type", "application/xml")
			_, _ = io.WriteString(w, emptyListResultXML)
			return
		}
		w.WriteHeader(http.StatusOK)
	}))
	t.Cleanup(server.Close)
	return strings.TrimPrefix(server.URL, "http://")
}

func newTestHandler(t *testing.T) http.Handler {
	t.Helper()
	for _, key := range serverlessEnvKeys {
		t.Setenv(key, "")
	}
	t.Setenv("S3_ENDPOINT", fakeS3(t))
	t.Setenv("S3_REGION", "us-east-1")
	t.Setenv("S3_BUCKET", "test-bucket")
	t.Setenv("S3_ACCESS_KEY", "access")
	t.Setenv("S3_SECRET_KEY", "secret")
	t.Setenv("S3_USE_SSL", "false")
	t.Setenv("S3_SSE", "false")
	t.Setenv("S3_LIFECYCLE_DAYS", "0")
	t.Setenv("INTERNAL_TOKEN", "internal-secret")

	handler, err := NewHandler(slog.New(slog.NewTextHandler(io.Discard, nil)))
	if err != nil {
		t.Fatalf("NewHandler() error = %v", err)
	}
	if handler == nil {
		t.Fatal("NewHandler() = nil")
	}
	return handler
}

func TestNewHandlerBuilds(t *testing.T) {
	if handler := newTestHandler(t); handler == nil {
		t.Fatal("handler is nil")
	}
}

func TestHandlerRoutesHealthz(t *testing.T) {
	handler := newTestHandler(t)
	rec := httptest.NewRecorder()
	handler.ServeHTTP(rec, httptest.NewRequest(http.MethodGet, "/healthz", nil))
	if rec.Code != http.StatusNoContent {
		t.Fatalf("GET /healthz = %d, want 204", rec.Code)
	}
}

func TestHandlerReapRequiresInternalToken(t *testing.T) {
	handler := newTestHandler(t)
	rec := httptest.NewRecorder()
	handler.ServeHTTP(rec, httptest.NewRequest(http.MethodPost, "/internal/snapshots/reap", nil))
	if rec.Code != http.StatusForbidden {
		t.Fatalf("POST /internal/snapshots/reap without token = %d, want 403", rec.Code)
	}
}

func TestHandlerReapWithInternalToken(t *testing.T) {
	handler := newTestHandler(t)
	req := httptest.NewRequest(http.MethodPost, "/internal/snapshots/reap", nil)
	req.Header.Set("X-Internal-Token", "internal-secret")
	rec := httptest.NewRecorder()
	handler.ServeHTTP(rec, req)

	if rec.Code == http.StatusForbidden {
		t.Fatal("POST /internal/snapshots/reap with token = 403, token rejected")
	}
	if ct := rec.Header().Get("Content-Type"); !strings.HasPrefix(ct, "application/json") {
		t.Fatalf("Content-Type = %q, want JSON", ct)
	}
	var body map[string]any
	if err := json.Unmarshal(rec.Body.Bytes(), &body); err != nil {
		t.Fatalf("response is not JSON: %v (%s)", err, rec.Body.String())
	}
	if rec.Code == http.StatusOK {
		if deleted, ok := body["deleted"].(float64); !ok || deleted != 0 {
			t.Fatalf("reap body = %v, want deleted=0", body)
		}
	} else if rec.Code != http.StatusBadGateway {
		t.Fatalf("reap status = %d, body = %v", rec.Code, body)
	}
}

func TestHandlerQueueMessages(t *testing.T) {
	handler := newTestHandler(t)
	cases := []struct {
		name  string
		token string
		body  string
		want  int
	}{
		{"missing token", "", `{"messages":[{"details":{"message":{"body":"{}"}}}]}`, http.StatusForbidden},
		{"bad envelope", "internal-secret", `{`, http.StatusBadRequest},
		{"no messages", "internal-secret", `{"messages":[]}`, http.StatusBadRequest},
	}
	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			req := httptest.NewRequest(http.MethodPost, "/internal/queue/messages", strings.NewReader(tc.body))
			if tc.token != "" {
				req.Header.Set("X-Internal-Token", tc.token)
			}
			rec := httptest.NewRecorder()
			handler.ServeHTTP(rec, req)
			if rec.Code != tc.want {
				t.Fatalf("POST /internal/queue/messages = %d, want %d (%s)", rec.Code, tc.want, rec.Body.String())
			}
		})
	}
}

func TestBreakerThreshold(t *testing.T) {
	cases := []struct {
		value string
		want  int
	}{
		{"", 5},
		{"9", 9},
		{"0", 5},
		{"-3", 5},
		{"many", 5},
	}
	for _, tc := range cases {
		t.Setenv("SOURCECRAFT_BREAKER_THRESHOLD", tc.value)
		if got := breakerThreshold(); got != tc.want {
			t.Errorf("breakerThreshold(%q) = %d, want %d", tc.value, got, tc.want)
		}
	}
}

func TestBreakerCooldown(t *testing.T) {
	cases := []struct {
		value string
		want  time.Duration
	}{
		{"", 30 * time.Second},
		{"45s", 45 * time.Second},
		{"0s", 30 * time.Second},
		{"-1m", 30 * time.Second},
		{"nope", 30 * time.Second},
	}
	for _, tc := range cases {
		t.Setenv("SOURCECRAFT_BREAKER_COOLDOWN", tc.value)
		if got := breakerCooldown(); got != tc.want {
			t.Errorf("breakerCooldown(%q) = %v, want %v", tc.value, got, tc.want)
		}
	}
}
