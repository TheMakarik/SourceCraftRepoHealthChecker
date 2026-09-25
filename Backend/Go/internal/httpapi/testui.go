package httpapi

import (
	_ "embed"
	"net/http"
)

// testUIPage — тестовая страница для ручной проверки API (без сборки и зависимостей).
//
//go:embed testui/index.html
var testUIPage []byte

func serveTestUI(w http.ResponseWriter, _ *http.Request) {
	w.Header().Set("Content-Type", "text/html; charset=utf-8")
	w.Header().Set("Cache-Control", "no-store")
	w.Header().Set("Content-Security-Policy", "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'unsafe-inline'; frame-ancestors 'none'")
	_, _ = w.Write(testUIPage)
}
