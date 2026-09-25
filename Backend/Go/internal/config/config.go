// Package config загружает настройки сервиса из переменных окружения.
package config

import (
	"errors"
	"fmt"
	"os"
	"strconv"
	"strings"
	"time"
)

type Config struct {
	HTTPAddr string
	// InternalToken защищает служебные эндпоинты (таймер-триггер очистки снапшотов).
	InternalToken string
	// ReapInterval > 0 — чистить просроченные снапшоты в процессе (локальный запуск без таймер-триггера).
	ReapInterval time.Duration

	SourceCraft SourceCraft
	YandexID    YandexID
	Storage     Storage
	Git         Git
}

// YandexID — OAuth-приложение для входа через Яндекс ID (регистрируется на https://oauth.yandex.ru).
type YandexID struct {
	ClientID     string
	ClientSecret string
	RedirectURI  string
	Scope        string
}

type SourceCraft struct {
	// BaseURL — корень публичного REST API SourceCraft.
	BaseURL string
	// ServiceToken — PAT сервиса для публичных данных (каталог, открытые репозитории).
	// Запросы пользователя идут с его собственным токеном из заголовка Authorization.
	ServiceToken string
	// GitUsername — имя пользователя для git по HTTPS. Пусто — берётся из GET /user.
	GitUsername string

	Timeout        time.Duration
	MaxRetries     int
	RequestsPerSec float64
}

type Storage struct {
	Endpoint  string
	Region    string
	Bucket    string
	AccessKey string
	SecretKey string
	UseSSL    bool
	// Encrypt включает SSE-S3 (AES256) для объектов снапшотов.
	Encrypt bool
	// Prefix — префикс ключей снапшотов в бакете.
	Prefix string
	// SnapshotTTL — сколько живёт снапшот, если его не удалили явно.
	SnapshotTTL time.Duration
	// LifecycleDays — страховочное lifecycle-правило бакета (S3 считает сроки в днях); 0 — не ставить.
	LifecycleDays int
}

type Git struct {
	Binary string
	// WorkDir — каталог для временных клонов; пусто — системный temp.
	WorkDir string
	// CloneTimeout ограничивает клонирование и упаковку одного репозитория.
	CloneTimeout time.Duration
	// MaxRepoBytes — верхняя граница размера клона; при превышении прогон прерывается.
	MaxRepoBytes int64
}

// MemoryStorage — значение S3_ENDPOINT для хранения снапшотов в памяти процесса (локальный запуск).
const MemoryStorage = "memory"

func Load() (Config, error) {
	var errs []error

	cfg := Config{
		HTTPAddr:      env("HTTP_ADDR", ":8080"),
		InternalToken: os.Getenv("INTERNAL_TOKEN"),
		ReapInterval:  duration("SNAPSHOT_REAP_INTERVAL", 0, &errs),
		SourceCraft: SourceCraft{
			BaseURL:        strings.TrimRight(env("SOURCECRAFT_API_URL", "https://api.sourcecraft.tech"), "/"),
			ServiceToken:   os.Getenv("SOURCECRAFT_TOKEN"),
			GitUsername:    os.Getenv("SOURCECRAFT_GIT_USERNAME"),
			Timeout:        duration("SOURCECRAFT_TIMEOUT", 15*time.Second, &errs),
			MaxRetries:     integer("SOURCECRAFT_MAX_RETRIES", 3, &errs),
			RequestsPerSec: float("SOURCECRAFT_RPS", 10, &errs),
		},
		YandexID: YandexID{
			ClientID:     os.Getenv("YANDEX_CLIENT_ID"),
			ClientSecret: os.Getenv("YANDEX_CLIENT_SECRET"),
			RedirectURI:  os.Getenv("YANDEX_REDIRECT_URI"),
			Scope:        os.Getenv("YANDEX_SCOPE"),
		},
		Storage: Storage{
			Endpoint:      os.Getenv("S3_ENDPOINT"),
			Region:        env("S3_REGION", "ru-central1"),
			Bucket:        os.Getenv("S3_BUCKET"),
			AccessKey:     os.Getenv("S3_ACCESS_KEY"),
			SecretKey:     os.Getenv("S3_SECRET_KEY"),
			UseSSL:        boolean("S3_USE_SSL", true, &errs),
			Encrypt:       boolean("S3_SSE", true, &errs),
			Prefix:        strings.Trim(env("S3_PREFIX", "snapshots"), "/"),
			SnapshotTTL:   duration("SNAPSHOT_TTL", 3*time.Hour, &errs),
			LifecycleDays: integer("S3_LIFECYCLE_DAYS", 1, &errs),
		},
		Git: Git{
			Binary:       env("GIT_BINARY", "git"),
			WorkDir:      os.Getenv("GIT_WORKDIR"),
			CloneTimeout: duration("GIT_CLONE_TIMEOUT", 15*time.Minute, &errs),
			MaxRepoBytes: int64(integer("GIT_MAX_REPO_MB", 2048, &errs)) << 20,
		},
	}

	if cfg.Storage.Endpoint == "" {
		errs = append(errs, errors.New("S3_ENDPOINT is required"))
	}
	if cfg.Storage.Bucket == "" && cfg.Storage.Endpoint != MemoryStorage {
		errs = append(errs, errors.New("S3_BUCKET is required"))
	}

	return cfg, errors.Join(errs...)
}

func env(key, fallback string) string {
	if v, ok := os.LookupEnv(key); ok && v != "" {
		return v
	}
	return fallback
}

func duration(key string, fallback time.Duration, errs *[]error) time.Duration {
	v := os.Getenv(key)
	if v == "" {
		return fallback
	}
	d, err := time.ParseDuration(v)
	if err != nil {
		*errs = append(*errs, fmt.Errorf("%s: %w", key, err))
		return fallback
	}
	return d
}

func integer(key string, fallback int, errs *[]error) int {
	v := os.Getenv(key)
	if v == "" {
		return fallback
	}
	n, err := strconv.Atoi(v)
	if err != nil {
		*errs = append(*errs, fmt.Errorf("%s: %w", key, err))
		return fallback
	}
	return n
}

func float(key string, fallback float64, errs *[]error) float64 {
	v := os.Getenv(key)
	if v == "" {
		return fallback
	}
	f, err := strconv.ParseFloat(v, 64)
	if err != nil {
		*errs = append(*errs, fmt.Errorf("%s: %w", key, err))
		return fallback
	}
	return f
}

func boolean(key string, fallback bool, errs *[]error) bool {
	v := os.Getenv(key)
	if v == "" {
		return fallback
	}
	b, err := strconv.ParseBool(v)
	if err != nil {
		*errs = append(*errs, fmt.Errorf("%s: %w", key, err))
		return fallback
	}
	return b
}
