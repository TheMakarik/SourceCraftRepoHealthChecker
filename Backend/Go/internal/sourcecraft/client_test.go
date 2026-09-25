package sourcecraft

import (
	"context"
	"errors"
	"net/http"
	"net/http/httptest"
	"strings"
	"sync/atomic"
	"testing"
	"time"
)

func newTestClient(t *testing.T, h http.HandlerFunc) *Client {
	t.Helper()
	srv := httptest.NewServer(h)
	t.Cleanup(srv.Close)
	return NewClient(Options{BaseURL: srv.URL, Timeout: 5 * time.Second, MaxRetries: 3})
}

func TestListAllFollowsPageTokens(t *testing.T) {
	c := newTestClient(t, func(w http.ResponseWriter, r *http.Request) {
		if got := r.Header.Get("Authorization"); got != "Bearer pat" {
			t.Errorf("Authorization = %q", got)
		}
		if r.URL.Path != "/repos/id:r1/releases" {
			t.Errorf("path = %q", r.URL.Path)
		}
		switch r.URL.Query().Get("page_token") {
		case "":
			_, _ = w.Write([]byte(`{"releases":[{"id":"1","status":"published"}],"next_page_token":"p2"}`))
		case "p2":
			_, _ = w.Write([]byte(`{"releases":[{"id":"2","status":"draft"}]}`))
		default:
			t.Errorf("unexpected page_token %q", r.URL.Query().Get("page_token"))
		}
	})

	got, err := c.Releases(context.Background(), "pat", "r1")
	if err != nil {
		t.Fatal(err)
	}
	if len(got) != 2 || got[0].ID != "1" || got[1].ID != "2" {
		t.Fatalf("releases = %+v", got)
	}
}

func TestListAllRespectsLimit(t *testing.T) {
	var calls atomic.Int32
	c := newTestClient(t, func(w http.ResponseWriter, r *http.Request) {
		calls.Add(1)
		_, _ = w.Write([]byte(`{"issues":[{"id":"a"},{"id":"b"}],"next_page_token":"more"}`))
	})

	got, err := c.Issues(context.Background(), "", "r1", 3)
	if err != nil {
		t.Fatal(err)
	}
	if len(got) != 3 || calls.Load() != 2 {
		t.Fatalf("got %d items in %d calls", len(got), calls.Load())
	}
}

func TestRetriesTransientErrors(t *testing.T) {
	var calls atomic.Int32
	c := newTestClient(t, func(w http.ResponseWriter, r *http.Request) {
		if calls.Add(1) < 3 {
			w.WriteHeader(http.StatusServiceUnavailable)
			return
		}
		_, _ = w.Write([]byte(`{"id":"r1","default_branch":"main"}`))
	})

	repo, err := c.Repository(context.Background(), "", "r1")
	if err != nil {
		t.Fatal(err)
	}
	if repo.DefaultBranch != "main" || calls.Load() != 3 {
		t.Fatalf("repo=%+v calls=%d", repo, calls.Load())
	}
}

func TestDoesNotRetryClientErrors(t *testing.T) {
	var calls atomic.Int32
	c := newTestClient(t, func(w http.ResponseWriter, r *http.Request) {
		calls.Add(1)
		w.WriteHeader(http.StatusNotFound)
		_, _ = w.Write([]byte(`{"error_code":"not_found","message":"no repo","request_id":"x"}`))
	})

	_, err := c.Repository(context.Background(), "", "missing")
	if !IsNotFound(err) {
		t.Fatalf("err = %v, want not found", err)
	}
	if calls.Load() != 1 {
		t.Fatalf("calls = %d, want 1", calls.Load())
	}
}

func TestExhaustedRetriesAreUnavailable(t *testing.T) {
	c := newTestClient(t, func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusBadGateway)
	})

	_, err := c.Repository(context.Background(), "", "r1")
	if !errors.Is(err, ErrUnavailable) {
		t.Fatalf("err = %v, want ErrUnavailable", err)
	}
}

func TestErrorsDoNotLeakToken(t *testing.T) {
	c := newTestClient(t, func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusUnauthorized)
		_, _ = w.Write([]byte(`{"error_code":"unauthorized","message":"Unauthorized"}`))
	})

	_, err := c.CurrentUser(context.Background(), "super-secret-pat")
	if !IsUnauthorized(err) {
		t.Fatalf("err = %v", err)
	}
	if strings.Contains(err.Error(), "super-secret-pat") {
		t.Fatal("token leaked into error message")
	}
}
