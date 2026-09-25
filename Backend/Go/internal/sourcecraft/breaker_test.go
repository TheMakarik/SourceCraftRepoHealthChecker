package sourcecraft

import (
	"errors"
	"io"
	"net/http"
	"strings"
	"sync/atomic"
	"testing"
	"time"
)

type roundTripFunc func(*http.Request) (*http.Response, error)

func (f roundTripFunc) RoundTrip(r *http.Request) (*http.Response, error) {
	return f(r)
}

func breakerResponse(status int) *http.Response {
	return &http.Response{StatusCode: status, Body: io.NopCloser(strings.NewReader("")), Header: http.Header{}}
}

func newBreakerRequest(t *testing.T) *http.Request {
	t.Helper()
	req, err := http.NewRequest(http.MethodGet, "https://api.sourcecraft.tech/repos", nil)
	if err != nil {
		t.Fatal(err)
	}
	return req
}

func TestBreakerOpensAfterThreshold(t *testing.T) {
	var calls atomic.Int32
	base := roundTripFunc(func(*http.Request) (*http.Response, error) {
		calls.Add(1)
		return breakerResponse(http.StatusBadGateway), nil
	})
	breaker := NewBreakerTransport(base, 2, time.Minute).(*breakerTransport)

	for range 2 {
		if _, err := breaker.RoundTrip(newBreakerRequest(t)); err != nil {
			t.Fatal(err)
		}
	}

	_, err := breaker.RoundTrip(newBreakerRequest(t))
	if !errors.Is(err, ErrBreakerOpen) {
		t.Fatalf("err = %v, want ErrBreakerOpen", err)
	}
	if calls.Load() != 2 {
		t.Fatalf("base calls = %d, want 2", calls.Load())
	}
}

func TestBreakerHalfOpenRecoversAfterCooldown(t *testing.T) {
	now := time.Unix(0, 0)
	var failing atomic.Bool
	failing.Store(true)
	base := roundTripFunc(func(*http.Request) (*http.Response, error) {
		if failing.Load() {
			return breakerResponse(http.StatusInternalServerError), nil
		}
		return breakerResponse(http.StatusOK), nil
	})
	breaker := NewBreakerTransport(base, 1, 30*time.Second).(*breakerTransport)
	breaker.now = func() time.Time { return now }

	if _, err := breaker.RoundTrip(newBreakerRequest(t)); err != nil {
		t.Fatal(err)
	}
	if _, err := breaker.RoundTrip(newBreakerRequest(t)); !errors.Is(err, ErrBreakerOpen) {
		t.Fatalf("err = %v, want ErrBreakerOpen", err)
	}

	failing.Store(false)
	now = now.Add(31 * time.Second)
	resp, err := breaker.RoundTrip(newBreakerRequest(t))
	if err != nil || resp.StatusCode != http.StatusOK {
		t.Fatalf("probe: resp=%v err=%v", resp, err)
	}
	if _, err := breaker.RoundTrip(newBreakerRequest(t)); err != nil {
		t.Fatalf("circuit did not close: %v", err)
	}
}

func TestBreakerReopensWhenProbeFails(t *testing.T) {
	now := time.Unix(0, 0)
	base := roundTripFunc(func(*http.Request) (*http.Response, error) {
		return breakerResponse(http.StatusInternalServerError), nil
	})
	breaker := NewBreakerTransport(base, 3, time.Minute).(*breakerTransport)
	breaker.now = func() time.Time { return now }

	if _, err := breaker.RoundTrip(newBreakerRequest(t)); err != nil {
		t.Fatal(err)
	}
	breaker.mu.Lock()
	breaker.openUntil = now.Add(time.Second)
	breaker.failures = 0
	breaker.mu.Unlock()

	now = now.Add(2 * time.Second)
	if _, err := breaker.RoundTrip(newBreakerRequest(t)); err != nil {
		t.Fatal(err)
	}
	if _, err := breaker.RoundTrip(newBreakerRequest(t)); !errors.Is(err, ErrBreakerOpen) {
		t.Fatalf("err = %v, want ErrBreakerOpen after failed probe", err)
	}
}

func TestBreakerIgnoresClientErrorsAndResets(t *testing.T) {
	var calls atomic.Int32
	status := atomic.Int32{}
	base := roundTripFunc(func(*http.Request) (*http.Response, error) {
		calls.Add(1)
		return breakerResponse(int(status.Load())), nil
	})
	breaker := NewBreakerTransport(base, 2, time.Minute)

	status.Store(http.StatusNotFound)
	for range 3 {
		if _, err := breaker.RoundTrip(newBreakerRequest(t)); err != nil {
			t.Fatal(err)
		}
	}
	status.Store(http.StatusBadGateway)
	if _, err := breaker.RoundTrip(newBreakerRequest(t)); err != nil {
		t.Fatal(err)
	}
	status.Store(http.StatusOK)
	if _, err := breaker.RoundTrip(newBreakerRequest(t)); err != nil {
		t.Fatal(err)
	}
	if _, err := breaker.RoundTrip(newBreakerRequest(t)); err != nil {
		t.Fatalf("success must reset failure count: %v", err)
	}
	if calls.Load() != 6 {
		t.Fatalf("base calls = %d, want 6", calls.Load())
	}
}

func TestBreakerCountsTransportErrors(t *testing.T) {
	base := roundTripFunc(func(*http.Request) (*http.Response, error) {
		return nil, errors.New("connection refused")
	})
	breaker := NewBreakerTransport(base, 1, time.Minute)

	if _, err := breaker.RoundTrip(newBreakerRequest(t)); err == nil || errors.Is(err, ErrBreakerOpen) {
		t.Fatalf("first call err = %v, want transport error", err)
	}
	if _, err := breaker.RoundTrip(newBreakerRequest(t)); !errors.Is(err, ErrBreakerOpen) {
		t.Fatalf("err = %v, want ErrBreakerOpen", err)
	}
}
