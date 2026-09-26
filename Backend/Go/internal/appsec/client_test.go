package appsec

import (
	"context"
	"errors"
	"fmt"
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

func TestDefectGroupsFollowsPagination(t *testing.T) {
	var calls atomic.Int32
	c := newTestClient(t, func(w http.ResponseWriter, r *http.Request) {
		calls.Add(1)
		if r.URL.Path != "/v1/defect-groups" {
			t.Errorf("path = %q", r.URL.Path)
		}
		if got := r.Header.Get("Authorization"); got != "Bearer pat" {
			t.Errorf("Authorization = %q", got)
		}
		if got := r.URL.Query().Get("gitRepo"); got != "r1" {
			t.Errorf("gitRepo = %q", got)
		}
		if got := r.URL.Query().Get("pageSize"); got != "250" {
			t.Errorf("pageSize = %q", got)
		}
		switch r.URL.Query().Get("pageToken") {
		case "":
			_, _ = w.Write([]byte(`{"data":[{"uuid":"a","ruleName":"A"}],"nextPageToken":"p2","totalSize":2}`))
		case "p2":
			_, _ = w.Write([]byte(`{"data":[{"uuid":"b","ruleName":"B"}],"totalSize":2}`))
		default:
			t.Errorf("unexpected pageToken %q", r.URL.Query().Get("pageToken"))
		}
	})

	page, err := c.DefectGroups(context.Background(), "pat", "r1", 0, "")
	if err != nil {
		t.Fatal(err)
	}
	if len(page.Items) != 2 || page.Items[0].UUID != "a" || page.Items[1].UUID != "b" {
		t.Fatalf("items = %+v", page.Items)
	}
	if page.TotalSize != 2 || page.NextPageToken != "" || calls.Load() != 2 {
		t.Fatalf("page = %+v calls = %d", page, calls.Load())
	}
}

func TestDefectGroupsRequiresGitRepo(t *testing.T) {
	c := newTestClient(t, func(w http.ResponseWriter, r *http.Request) {
		t.Error("no request expected")
	})
	if _, err := c.DefectGroups(context.Background(), "pat", "", 0, ""); err == nil {
		t.Fatal("want error for empty gitRepo")
	}
}

func TestDefectGroupsMapsNotFound(t *testing.T) {
	var calls atomic.Int32
	c := newTestClient(t, func(w http.ResponseWriter, r *http.Request) {
		calls.Add(1)
		w.WriteHeader(http.StatusNotFound)
		_, _ = w.Write([]byte(`{"error":"Entity not found: repository=missing"}`))
	})

	_, err := c.DefectGroups(context.Background(), "pat", "missing", 0, "")
	if !IsNotFound(err) {
		t.Fatalf("err = %v, want not found", err)
	}
	if calls.Load() != 1 {
		t.Fatalf("calls = %d, want 1", calls.Load())
	}
}

func TestDefectGroupsRetriesTransient(t *testing.T) {
	var calls atomic.Int32
	c := newTestClient(t, func(w http.ResponseWriter, r *http.Request) {
		if calls.Add(1) < 3 {
			w.WriteHeader(http.StatusServiceUnavailable)
			return
		}
		_, _ = w.Write([]byte(`{"data":[{"uuid":"a"}]}`))
	})

	page, err := c.DefectGroups(context.Background(), "pat", "r1", 10, "")
	if err != nil {
		t.Fatal(err)
	}
	if len(page.Items) != 1 || calls.Load() != 3 {
		t.Fatalf("page = %+v calls = %d", page, calls.Load())
	}
}

func TestDefectGroupsExhaustedRetriesAreUnavailable(t *testing.T) {
	c := newTestClient(t, func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusBadGateway)
	})

	_, err := c.DefectGroups(context.Background(), "pat", "r1", 0, "")
	if !errors.Is(err, ErrUnavailable) {
		t.Fatalf("err = %v, want ErrUnavailable", err)
	}
}

func TestDefectGroupsStopsOnRepeatedPageToken(t *testing.T) {
	var calls atomic.Int32
	c := newTestClient(t, func(w http.ResponseWriter, r *http.Request) {
		calls.Add(1)
		_, _ = w.Write([]byte(`{"data":[{"uuid":"a"}],"nextPageToken":"same"}`))
	})

	page, err := c.DefectGroups(context.Background(), "pat", "r1", 10, "")
	if err != nil {
		t.Fatal(err)
	}
	if len(page.Items) != 1 || page.NextPageToken != "same" || calls.Load() != 2 {
		t.Fatalf("page = %+v calls = %d", page, calls.Load())
	}
}

func TestDefectGroupsStopsOnPageTokenCycle(t *testing.T) {
	var calls atomic.Int32
	c := newTestClient(t, func(w http.ResponseWriter, r *http.Request) {
		calls.Add(1)
		next := "p2"
		switch r.URL.Query().Get("pageToken") {
		case "p2":
			next = "p3"
		case "p3":
			next = "p2"
		}
		_, _ = w.Write([]byte(`{"data":[{"uuid":"a"}],"nextPageToken":"` + next + `","totalSize":1}`))
	})

	page, err := c.DefectGroups(context.Background(), "pat", "r1", 10, "")
	if err != nil {
		t.Fatal(err)
	}
	if calls.Load() != 3 {
		t.Fatalf("calls = %d, want 3 (p2 -> p3 -> p2 must stop)", calls.Load())
	}
	if len(page.Items) != 2 {
		t.Fatalf("items = %d, want 2: repeated page must not be appended", len(page.Items))
	}
}

func TestDefectGroupsCapsPages(t *testing.T) {
	var calls atomic.Int32
	c := newTestClient(t, func(w http.ResponseWriter, r *http.Request) {
		n := calls.Add(1)
		_, _ = w.Write([]byte(fmt.Sprintf(`{"data":[{"uuid":"a"}],"nextPageToken":"p%d","totalSize":1}`, n+1)))
	})

	page, err := c.DefectGroups(context.Background(), "pat", "r1", 10, "")
	if err != nil {
		t.Fatal(err)
	}
	if calls.Load() != maxPages {
		t.Fatalf("calls = %d, want %d", calls.Load(), maxPages)
	}
	if len(page.Items) != maxPages {
		t.Fatalf("items = %d, want %d", len(page.Items), maxPages)
	}
}

func TestErrorsDoNotLeakToken(t *testing.T) {
	c := newTestClient(t, func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusUnauthorized)
		_, _ = w.Write([]byte(`{"error":"Unauthorized"}`))
	})

	_, err := c.DefectGroups(context.Background(), "super-secret-pat", "r1", 0, "")
	if !IsUnauthorized(err) {
		t.Fatalf("err = %v", err)
	}
	if strings.Contains(err.Error(), "super-secret-pat") {
		t.Fatal("token leaked into error message")
	}
}
