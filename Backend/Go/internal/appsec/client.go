// Package appsec — клиент AppSec API SourceCraft (https://appsec.sourcecraft.tech).
//
// Клиент только транспортирует данные: ретраи с backoff, учёт Retry-After и пагинация.
// Интерпретация находок — в пакете service. Токен не логируется и не попадает в ошибки.
package appsec

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

const (
	defaultBaseURL = "https://appsec.sourcecraft.tech"
	maxPageSize    = 250
	// maxPages ограничивает пагинацию, чтобы недоступный или зацикленный nextPageToken не крутил цикл вечно.
	maxPages = 100
)

type Options struct {
	BaseURL    string
	Timeout    time.Duration
	MaxRetries int
	HTTPClient *http.Client
}

type Client struct {
	baseURL    string
	http       *http.Client
	maxRetries int
}

func NewClient(opts Options) *Client {
	baseURL := opts.BaseURL
	if baseURL == "" {
		baseURL = defaultBaseURL
	}
	hc := opts.HTTPClient
	if hc == nil {
		hc = &http.Client{Timeout: opts.Timeout}
	}
	return &Client{
		baseURL:    strings.TrimRight(baseURL, "/"),
		http:       hc,
		maxRetries: opts.MaxRetries,
	}
}

// APIError — ответ AppSec с кодом не 2xx.
type APIError struct {
	StatusCode int
	Message    string
}

func (e *APIError) Error() string {
	return fmt.Sprintf("appsec: %d: %s", e.StatusCode, e.Message)
}

// ErrUnavailable — AppSec недоступен (сеть, 5xx, исчерпаны ретраи).
var ErrUnavailable = errors.New("appsec: unavailable")

func IsNotFound(err error) bool     { return statusIs(err, http.StatusNotFound) }
func IsForbidden(err error) bool    { return statusIs(err, http.StatusForbidden) }
func IsUnauthorized(err error) bool { return statusIs(err, http.StatusUnauthorized) }

func statusIs(err error, code int) bool {
	var apiErr *APIError
	return errors.As(err, &apiErr) && apiErr.StatusCode == code
}

// Page — накопленный результат обхода страниц списка дефектов.
type Page struct {
	Items         []DefectGroupDto
	NextPageToken string
	TotalSize     int
}

// DefectGroups забирает группы дефектов через GET /v1/defect-groups, начиная с pageToken,
// и идёт по nextPageToken до maxPages страниц. Возвращённый NextPageToken непуст, если
// пагинация остановлена на лимите страниц и данные можно добрать повторным вызовом.
func (c *Client) DefectGroups(ctx context.Context, token, gitRepo string, pageSize int, pageToken string) (Page, error) {
	if gitRepo == "" {
		return Page{}, errors.New("appsec: gitRepo is required")
	}
	if pageSize <= 0 || pageSize > maxPageSize {
		pageSize = maxPageSize
	}

	var (
		all   []DefectGroupDto
		next  = pageToken
		total int
	)
	for page := 0; page < maxPages; page++ {
		q := url.Values{}
		q.Set("gitRepo", gitRepo)
		q.Set("pageSize", strconv.Itoa(pageSize))
		if next != "" {
			q.Set("pageToken", next)
		}

		var resp PaginatedDefectGroupsDto
		if err := c.get(ctx, token, "/v1/defect-groups", q, &resp); err != nil {
			return Page{}, err
		}
		if resp.NextPageToken == "" {
			all = append(all, resp.Data...)
			return Page{Items: all, TotalSize: resp.TotalSize}, nil
		}
		if resp.NextPageToken == next {
			return Page{Items: all, NextPageToken: resp.NextPageToken, TotalSize: total}, nil
		}
		all = append(all, resp.Data...)
		total = resp.TotalSize
		next = resp.NextPageToken
	}
	return Page{Items: all, NextPageToken: next, TotalSize: total}, nil
}

// get выполняет GET path?query и декодирует JSON в out. Токен не логируется и не попадает в ошибки.
func (c *Client) get(ctx context.Context, token, path string, query url.Values, out any) error {
	u := c.baseURL + path
	if len(query) > 0 {
		u += "?" + query.Encode()
	}

	var lastErr error
	for attempt := 0; ; attempt++ {
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
			return -1, fmt.Errorf("appsec: decode %s: %w", req.URL.Path, err)
		}
		return -1, nil
	}

	apiErr := &APIError{StatusCode: resp.StatusCode}
	var body struct {
		Error   string `json:"error"`
		Message string `json:"message"`
	}
	if json.NewDecoder(io.LimitReader(resp.Body, 64<<10)).Decode(&body) == nil {
		apiErr.Message = body.Error
		if apiErr.Message == "" {
			apiErr.Message = body.Message
		}
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
