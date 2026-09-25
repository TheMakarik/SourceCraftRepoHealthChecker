package yandexid

import (
	"context"
	"errors"
	"net/http"
	"net/http/httptest"
	"net/url"
	"strings"
	"testing"
)

// fakeYandex принимает код "good-code" и выдаёт токен "ya-token" пользователю 42.
func fakeYandex(t *testing.T) *httptest.Server {
	t.Helper()
	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		switch r.URL.Path {
		case "/token":
			id, secret, ok := r.BasicAuth()
			if !ok || id != "app-id" || secret != "app-secret" {
				w.WriteHeader(http.StatusUnauthorized)
				_, _ = w.Write([]byte(`{"error":"invalid_client","error_description":"Client not found"}`))
				return
			}
			_ = r.ParseForm()
			if r.PostForm.Get("grant_type") != "authorization_code" || r.PostForm.Get("code") != "good-code" {
				w.WriteHeader(http.StatusBadRequest)
				_, _ = w.Write([]byte(`{"error":"invalid_grant","error_description":"Code has expired"}`))
				return
			}
			_, _ = w.Write([]byte(`{"access_token":"ya-token","token_type":"bearer","expires_in":31536000}`))
		case "/info":
			if r.Header.Get("Authorization") != "OAuth ya-token" || r.URL.Query().Get("format") != "json" {
				w.WriteHeader(http.StatusUnauthorized)
				return
			}
			_, _ = w.Write([]byte(`{"id":"42","login":"ivan","display_name":"Ivan","default_email":"ivan@yandex.ru"}`))
		default:
			w.WriteHeader(http.StatusNotFound)
		}
	}))
	t.Cleanup(srv.Close)
	return srv
}

func newClient(srv *httptest.Server, secret string) *Client {
	return New(Options{ClientID: "app-id", ClientSecret: secret, OAuthURL: srv.URL, LoginURL: srv.URL})
}

func TestAuthorizationURL(t *testing.T) {
	c := New(Options{ClientID: "app-id", ClientSecret: "s", RedirectURI: "https://app.example/callback", Scope: "login:info login:email"})
	u, err := url.Parse(c.AuthorizationURL("st&ate"))
	if err != nil {
		t.Fatal(err)
	}
	q := u.Query()
	if u.Host != "oauth.yandex.ru" || u.Path != "/authorize" ||
		q.Get("response_type") != "code" || q.Get("client_id") != "app-id" || q.Get("state") != "st&ate" ||
		q.Get("redirect_uri") != "https://app.example/callback" || q.Get("scope") != "login:info login:email" {
		t.Fatalf("url = %s", u)
	}
}

func TestAuthenticate(t *testing.T) {
	u, err := newClient(fakeYandex(t), "app-secret").Authenticate(context.Background(), "good-code")
	if err != nil {
		t.Fatal(err)
	}
	if u != (User{ID: "42", Login: "ivan", DisplayName: "Ivan", DefaultEmail: "ivan@yandex.ru"}) {
		t.Fatalf("user = %+v", u)
	}
}

func TestAuthenticateErrors(t *testing.T) {
	srv := fakeYandex(t)

	if _, err := newClient(srv, "app-secret").Authenticate(context.Background(), "stale-code"); !errors.Is(err, ErrInvalidGrant) {
		t.Fatalf("stale code: err = %v", err)
	}

	_, err := newClient(srv, "wrong-secret").Authenticate(context.Background(), "good-code")
	if err == nil || errors.Is(err, ErrInvalidGrant) || !strings.Contains(err.Error(), "invalid_client") {
		t.Fatalf("bad client: err = %v", err)
	}
	if strings.Contains(err.Error(), "wrong-secret") {
		t.Fatal("client secret leaked into error")
	}

	srv.Close()
	if _, err := newClient(srv, "app-secret").Authenticate(context.Background(), "good-code"); !errors.Is(err, ErrUnavailable) {
		t.Fatalf("down: err = %v", err)
	}
}
