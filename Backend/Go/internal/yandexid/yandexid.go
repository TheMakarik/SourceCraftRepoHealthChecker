// Package yandexid — вход через Яндекс ID (OAuth 2.0 authorization code flow).
//
// Токен Яндекс ID подтверждает только личность пользователя: API SourceCraft его не принимает
// (нужен PAT или IAM-токен Yandex Cloud, а обмен OAuth-токенов на IAM закрыт для новых токенов
// с 01.06.2026). Поэтому токен используется один раз — чтобы прочитать профиль — и наружу не отдаётся.
//
// Документация: https://yandex.ru/dev/id/doc/ru/codes/code-url, https://yandex.ru/dev/id/doc/ru/user-information
package yandexid

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"strings"
	"time"
)

type Options struct {
	ClientID     string
	ClientSecret string
	// RedirectURI — callback C#-бэкенда, зарегистрированный в приложении на oauth.yandex.ru.
	// Пусто — Яндекс использует единственный зарегистрированный callback.
	RedirectURI string
	// Scope — права через пробел (например, "login:info login:email"). Пусто — права из настроек приложения.
	Scope string

	OAuthURL   string // по умолчанию https://oauth.yandex.ru
	LoginURL   string // по умолчанию https://login.yandex.ru
	HTTPClient *http.Client
}

type Client struct {
	opts Options
	http *http.Client
}

func New(opts Options) *Client {
	if opts.OAuthURL == "" {
		opts.OAuthURL = "https://oauth.yandex.ru"
	}
	if opts.LoginURL == "" {
		opts.LoginURL = "https://login.yandex.ru"
	}
	opts.OAuthURL = strings.TrimRight(opts.OAuthURL, "/")
	opts.LoginURL = strings.TrimRight(opts.LoginURL, "/")
	hc := opts.HTTPClient
	if hc == nil {
		hc = &http.Client{Timeout: 10 * time.Second}
	}
	return &Client{opts: opts, http: hc}
}

// Configured сообщает, заданы ли креды OAuth-приложения.
func (c *Client) Configured() bool {
	return c.opts.ClientID != "" && c.opts.ClientSecret != ""
}

// AuthorizationURL строит ссылку на страницу входа Яндекс ID. state генерирует и проверяет вызывающая сторона (C#).
func (c *Client) AuthorizationURL(state string) string {
	q := url.Values{
		"response_type": {"code"},
		"client_id":     {c.opts.ClientID},
		"state":         {state},
	}
	if c.opts.RedirectURI != "" {
		q.Set("redirect_uri", c.opts.RedirectURI)
	}
	if c.opts.Scope != "" {
		q.Set("scope", c.opts.Scope)
	}
	return c.opts.OAuthURL + "/authorize?" + q.Encode()
}

// ErrInvalidGrant — код подтверждения неверный, просрочен (живёт 10 минут) или уже использован.
var ErrInvalidGrant = errors.New("yandexid: invalid or expired authorization code")

// ErrUnavailable — Яндекс ID недоступен.
var ErrUnavailable = errors.New("yandexid: unavailable")

type User struct {
	ID           string
	Login        string
	DisplayName  string
	DefaultEmail string
}

// Authenticate обменивает код подтверждения на токен и возвращает профиль пользователя.
func (c *Client) Authenticate(ctx context.Context, code string) (User, error) {
	token, err := c.exchange(ctx, code)
	if err != nil {
		return User{}, err
	}
	return c.userInfo(ctx, token)
}

func (c *Client) exchange(ctx context.Context, code string) (string, error) {
	form := url.Values{
		"grant_type": {"authorization_code"},
		"code":       {code},
	}
	req, err := http.NewRequestWithContext(ctx, http.MethodPost, c.opts.OAuthURL+"/token", strings.NewReader(form.Encode()))
	if err != nil {
		return "", err
	}
	req.Header.Set("Content-Type", "application/x-www-form-urlencoded")
	req.SetBasicAuth(c.opts.ClientID, c.opts.ClientSecret)

	resp, err := c.http.Do(req)
	if err != nil {
		return "", c.transportErr(ctx, err)
	}
	defer resp.Body.Close()

	var body struct {
		AccessToken      string `json:"access_token"`
		Error            string `json:"error"`
		ErrorDescription string `json:"error_description"`
	}
	if err := json.NewDecoder(io.LimitReader(resp.Body, 64<<10)).Decode(&body); err != nil && resp.StatusCode == http.StatusOK {
		return "", fmt.Errorf("yandexid: decode token response: %w", err)
	}

	switch {
	case resp.StatusCode == http.StatusOK && body.AccessToken != "":
		return body.AccessToken, nil
	case body.Error == "invalid_grant" || body.Error == "bad_verification_code":
		return "", ErrInvalidGrant
	case resp.StatusCode >= 500:
		return "", fmt.Errorf("%w: token endpoint returned %d", ErrUnavailable, resp.StatusCode)
	default:
		// invalid_client и т.п. — ошибка конфигурации приложения, а не пользователя.
		return "", fmt.Errorf("yandexid: token exchange failed: %d %s: %s", resp.StatusCode, body.Error, body.ErrorDescription)
	}
}

func (c *Client) userInfo(ctx context.Context, token string) (User, error) {
	req, err := http.NewRequestWithContext(ctx, http.MethodGet, c.opts.LoginURL+"/info?format=json", nil)
	if err != nil {
		return User{}, err
	}
	req.Header.Set("Authorization", "OAuth "+token)

	resp, err := c.http.Do(req)
	if err != nil {
		return User{}, c.transportErr(ctx, err)
	}
	defer resp.Body.Close()

	switch {
	case resp.StatusCode >= 500:
		return User{}, fmt.Errorf("%w: user info returned %d", ErrUnavailable, resp.StatusCode)
	case resp.StatusCode != http.StatusOK:
		return User{}, fmt.Errorf("yandexid: user info returned %d", resp.StatusCode)
	}

	var info struct {
		ID           string `json:"id"`
		Login        string `json:"login"`
		DisplayName  string `json:"display_name"`
		RealName     string `json:"real_name"`
		DefaultEmail string `json:"default_email"`
	}
	if err := json.NewDecoder(io.LimitReader(resp.Body, 64<<10)).Decode(&info); err != nil {
		return User{}, fmt.Errorf("yandexid: decode user info: %w", err)
	}
	if info.ID == "" {
		return User{}, errors.New("yandexid: user info has no id")
	}
	name := info.DisplayName
	if name == "" {
		name = info.RealName
	}
	if name == "" {
		name = info.Login
	}
	return User{ID: info.ID, Login: info.Login, DisplayName: name, DefaultEmail: info.DefaultEmail}, nil
}

func (c *Client) transportErr(ctx context.Context, err error) error {
	if ctx.Err() != nil {
		return ctx.Err()
	}
	return fmt.Errorf("%w: %w", ErrUnavailable, err)
}
