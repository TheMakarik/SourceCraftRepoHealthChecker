// Package sourcecraft — клиент публичного REST API SourceCraft (https://api.sourcecraft.tech).
//
// Клиент только транспортирует данные: ретраи, ограничение частоты, пагинация
// и типизированные ошибки. Интерпретация фактов — в пакете service.
package sourcecraft

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"math/rand/v2"
	"net/http"
	"net/url"
	"strconv"
	"strings"
	"time"
)

const maxPageSize = 100

type Options struct {
	BaseURL        string
	Timeout        time.Duration
	MaxRetries     int
	RequestsPerSec float64
	HTTPClient     *http.Client
}

type Client struct {
	baseURL    string
	http       *http.Client
	maxRetries int
	limiter    *limiter
}

func NewClient(opts Options) *Client {
	hc := opts.HTTPClient
	if hc == nil {
		hc = &http.Client{Timeout: opts.Timeout}
	}
	return &Client{
		baseURL:    strings.TrimRight(opts.BaseURL, "/"),
		http:       hc,
		maxRetries: opts.MaxRetries,
		limiter:    newLimiter(opts.RequestsPerSec),
	}
}

// APIError — ответ SourceCraft с кодом не 2xx.
type APIError struct {
	StatusCode int
	Code       string
	Message    string
	RequestID  string
}

func (e *APIError) Error() string {
	return fmt.Sprintf("sourcecraft: %d %s: %s (request_id=%s)", e.StatusCode, e.Code, e.Message, e.RequestID)
}

// ErrUnavailable — SourceCraft недоступен (сеть, 5xx, исчерпаны ретраи).
var ErrUnavailable = errors.New("sourcecraft: unavailable")

func IsNotFound(err error) bool     { return statusIs(err, http.StatusNotFound) }
func IsForbidden(err error) bool    { return statusIs(err, http.StatusForbidden) }
func IsUnauthorized(err error) bool { return statusIs(err, http.StatusUnauthorized) }

func statusIs(err error, code int) bool {
	var apiErr *APIError
	return errors.As(err, &apiErr) && apiErr.StatusCode == code
}

// get выполняет GET path?query и декодирует JSON в out. Токен не логируется и не попадает в ошибки.
func (c *Client) get(ctx context.Context, token, path string, query url.Values, out any) error {
	u := c.baseURL + path
	if len(query) > 0 {
		u += "?" + query.Encode()
	}

	var lastErr error
	for attempt := 0; ; attempt++ {
		if err := c.limiter.wait(ctx); err != nil {
			return err
		}

		retryAfter, err := c.do(ctx, token, u, out)
		if err == nil {
			return nil
		}
		lastErr = err
		if retryAfter < 0 || attempt >= c.maxRetries {
			break
		}

		delay := backoff(attempt)
		if retryAfter > delay {
			delay = retryAfter
		}
		timer := time.NewTimer(delay)
		select {
		case <-ctx.Done():
			timer.Stop()
			return ctx.Err()
		case <-timer.C:
		}
	}

	var apiErr *APIError
	if errors.As(lastErr, &apiErr) && apiErr.StatusCode < 500 && apiErr.StatusCode != http.StatusTooManyRequests {
		return lastErr
	}
	if ctx.Err() != nil {
		return ctx.Err()
	}
	return fmt.Errorf("%w: %w", ErrUnavailable, lastErr)
}

// do делает одну попытку. retryAfter < 0 — повтор бессмысленен.
func (c *Client) do(ctx context.Context, token, u string, out any) (retryAfter time.Duration, err error) {
	req, err := http.NewRequestWithContext(ctx, http.MethodGet, u, nil)
	if err != nil {
		return -1, err
	}
	req.Header.Set("Accept", "application/json")
	if token != "" {
		req.Header.Set("Authorization", "Bearer "+token)
	}

	resp, err := c.http.Do(req)
	if err != nil {
		if ctx.Err() != nil {
			return -1, ctx.Err()
		}
		return 0, err
	}
	defer resp.Body.Close()

	if resp.StatusCode >= 200 && resp.StatusCode < 300 {
		if out == nil {
			_, _ = io.Copy(io.Discard, resp.Body)
			return -1, nil
		}
		if err := json.NewDecoder(resp.Body).Decode(out); err != nil {
			return -1, fmt.Errorf("sourcecraft: decode %s: %w", req.URL.Path, err)
		}
		return -1, nil
	}

	apiErr := &APIError{StatusCode: resp.StatusCode}
	var body struct {
		ErrorCode string `json:"error_code"`
		Message   string `json:"message"`
		RequestID string `json:"request_id"`
	}
	if json.NewDecoder(io.LimitReader(resp.Body, 64<<10)).Decode(&body) == nil {
		apiErr.Code, apiErr.Message, apiErr.RequestID = body.ErrorCode, body.Message, body.RequestID
	}

	switch {
	case resp.StatusCode == http.StatusTooManyRequests || resp.StatusCode >= 500:
		return parseRetryAfter(resp.Header.Get("Retry-After")), apiErr
	default:
		return -1, apiErr
	}
}

func backoff(attempt int) time.Duration {
	base := 200 * time.Millisecond << attempt
	if base > 5*time.Second {
		base = 5 * time.Second
	}
	return base/2 + time.Duration(rand.Int64N(int64(base/2)+1))
}

func parseRetryAfter(v string) time.Duration {
	if v == "" {
		return 0
	}
	if secs, err := strconv.Atoi(v); err == nil {
		return time.Duration(secs) * time.Second
	}
	if t, err := http.ParseTime(v); err == nil {
		return time.Until(t)
	}
	return 0
}

// Page — одна страница списка SourceCraft.
type Page[T any] struct {
	Items         []T
	NextPageToken string
}

// listAll проходит все страницы, пока fetch не вернёт пустой токен или не наберётся limit элементов (limit <= 0 — без ограничения).
func listAll[T any](ctx context.Context, limit int, fetch func(ctx context.Context, pageToken string) (Page[T], error)) ([]T, error) {
	var (
		all   []T
		token string
	)
	for {
		page, err := fetch(ctx, token)
		if err != nil {
			return nil, err
		}
		all = append(all, page.Items...)
		if limit > 0 && len(all) >= limit {
			return all[:limit], nil
		}
		if page.NextPageToken == "" || page.NextPageToken == token {
			return all, nil
		}
		token = page.NextPageToken
	}
}

func pageQuery(pageToken string, extra url.Values) url.Values {
	q := url.Values{}
	for k, v := range extra {
		q[k] = v
	}
	q.Set("page_size", strconv.Itoa(maxPageSize))
	if pageToken != "" {
		q.Set("page_token", pageToken)
	}
	return q
}

// limiter — простой token bucket, чтобы не превышать лимиты SourceCraft.
type limiter struct {
	tokens chan struct{}
}

func newLimiter(rps float64) *limiter {
	if rps <= 0 {
		return &limiter{}
	}
	burst := max(1, int(rps))
	l := &limiter{tokens: make(chan struct{}, burst)}
	for range burst {
		l.tokens <- struct{}{}
	}
	go func() {
		t := time.NewTicker(time.Duration(float64(time.Second) / rps))
		defer t.Stop()
		for range t.C {
			select {
			case l.tokens <- struct{}{}:
			default:
			}
		}
	}()
	return l
}

func (l *limiter) wait(ctx context.Context) error {
	if l.tokens == nil {
		return nil
	}
	select {
	case <-l.tokens:
		return nil
	case <-ctx.Done():
		return ctx.Err()
	}
}
