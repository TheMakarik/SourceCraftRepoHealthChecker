package sourcecraft

import (
	"errors"
	"net/http"
	"sync"
	"time"
)

// ErrBreakerOpen — цепь разомкнута: источник считается недоступным, запрос не уходит.
var ErrBreakerOpen = errors.New("sourcecraft: circuit breaker is open")

const (
	defaultBreakerThreshold = 5
	defaultBreakerCooldown  = 30 * time.Second
)

type breakerTransport struct {
	base      http.RoundTripper
	threshold int
	cooldown  time.Duration
	now       func() time.Time

	mu        sync.Mutex
	failures  int
	openUntil time.Time
	probing   bool
}

// NewBreakerTransport оборачивает base в circuit breaker: после threshold подряд идущих
// сбоев (ошибка транспорта, 5xx, 429) запросы отклоняются cooldown, затем пропускается
// одна пробная попытка.
func NewBreakerTransport(base http.RoundTripper, threshold int, cooldown time.Duration) http.RoundTripper {
	if base == nil {
		base = http.DefaultTransport
	}
	if threshold <= 0 {
		threshold = defaultBreakerThreshold
	}
	if cooldown <= 0 {
		cooldown = defaultBreakerCooldown
	}
	return &breakerTransport{base: base, threshold: threshold, cooldown: cooldown, now: time.Now}
}

func (b *breakerTransport) RoundTrip(req *http.Request) (*http.Response, error) {
	if !b.allow() {
		return nil, ErrBreakerOpen
	}

	resp, err := b.base.RoundTrip(req)
	switch {
	case err != nil:
		b.fail()
		return nil, err
	case isBreakerFailure(resp.StatusCode):
		b.fail()
	default:
		b.succeed()
	}
	return resp, nil
}

// allow сообщает, можно ли выполнить запрос: цепь замкнута, либо истёк cooldown и это
// единственная пробная попытка.
func (b *breakerTransport) allow() bool {
	b.mu.Lock()
	defer b.mu.Unlock()
	if b.openUntil.IsZero() {
		return true
	}
	if b.now().Before(b.openUntil) {
		return false
	}
	if b.probing {
		return false
	}
	b.probing = true
	return true
}

func (b *breakerTransport) fail() {
	b.mu.Lock()
	defer b.mu.Unlock()
	wasProbing := b.probing
	b.probing = false
	b.failures++
	if wasProbing || b.failures >= b.threshold {
		b.openUntil = b.now().Add(b.cooldown)
		b.failures = 0
	}
}

func (b *breakerTransport) succeed() {
	b.mu.Lock()
	defer b.mu.Unlock()
	b.failures = 0
	b.probing = false
	b.openUntil = time.Time{}
}

func isBreakerFailure(status int) bool {
	return status >= http.StatusInternalServerError || status == http.StatusTooManyRequests
}
