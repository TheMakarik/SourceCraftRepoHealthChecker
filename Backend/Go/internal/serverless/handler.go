// Package serverless собирает HTTP-обработчик для точки входа Cloud Functions:
// те же маршруты, что и у обычного сервера, плюс триггеры Message Queue и таймера.
package serverless

import (
	"context"
	"log/slog"
	"net/http"
	"os"
	"strconv"
	"time"

	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/appsec"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/config"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/gitrepo"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/httpapi"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/objectstore"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/queue"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/service"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/snapshot"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/sourcecraft"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/yandexid"
)

func NewHandler(log *slog.Logger) (http.Handler, error) {
	cfg, err := config.Load()
	if err != nil {
		return nil, err
	}

	objects, err := objectstore.NewS3(objectstore.S3Options{
		Endpoint:  cfg.Storage.Endpoint,
		Region:    cfg.Storage.Region,
		Bucket:    cfg.Storage.Bucket,
		AccessKey: cfg.Storage.AccessKey,
		SecretKey: cfg.Storage.SecretKey,
		UseSSL:    cfg.Storage.UseSSL,
		Encrypt:   cfg.Storage.Encrypt,
	})
	if err != nil {
		return nil, err
	}
	initCtx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	err = objects.EnsureBucket(initCtx, cfg.Storage.Region, cfg.Storage.Prefix, cfg.Storage.LifecycleDays)
	cancel()
	if err != nil {
		// Холодный старт не должен падать из-за временно недоступного хранилища.
		log.Warn("object storage init failed", "err", err)
	}

	git := gitrepo.Git{Binary: cfg.Git.Binary, Timeout: cfg.Git.CommandTimeout}
	snapshots := snapshot.NewStore(objects, git, snapshot.Options{
		Prefix:       cfg.Storage.Prefix,
		TTL:          cfg.Storage.SnapshotTTL,
		WorkDir:      cfg.Git.WorkDir,
		MaxRepoBytes: cfg.Git.MaxRepoBytes,
		CloneTimeout: cfg.Git.CloneTimeout,
	}, log)

	api := sourcecraft.NewClient(sourcecraft.Options{
		BaseURL:        cfg.SourceCraft.BaseURL,
		Timeout:        cfg.SourceCraft.Timeout,
		MaxRetries:     cfg.SourceCraft.MaxRetries,
		RequestsPerSec: cfg.SourceCraft.RequestsPerSec,
		HTTPClient: &http.Client{
			Timeout: cfg.SourceCraft.Timeout,
			Transport: sourcecraft.NewBreakerTransport(
				http.DefaultTransport, breakerThreshold(), breakerCooldown()),
		},
	})
	appSec := appsec.NewClient(appsec.Options{
		BaseURL:    cfg.AppSec.BaseURL,
		Timeout:    cfg.AppSec.Timeout,
		MaxRetries: cfg.AppSec.MaxRetries,
	})
	svc := service.New(api, appSec, snapshots, git, cfg.SourceCraft.GitUsername, cfg.Git.WorkDir, service.Limits{
		MaxItems:           500,
		MaxResponseLookups: 100,
		Concurrency:        4,
	})

	main := httpapi.New(svc, httpapi.Options{
		ServiceToken:  cfg.SourceCraft.ServiceToken,
		InternalToken: cfg.InternalToken,
		YandexID: yandexid.New(yandexid.Options{
			ClientID:     cfg.YandexID.ClientID,
			ClientSecret: cfg.YandexID.ClientSecret,
			RedirectURI:  cfg.YandexID.RedirectURI,
			Scope:        cfg.YandexID.Scope,
		}),
	}, log)

	consumer := queue.NewConsumer(svc, cfg.SourceCraft.ServiceToken, cfg.InternalToken, log)
	timer := NewTimer(svc, cfg.InternalToken, log)

	// Внешний mux: триггеры с приоритетом, остальные маршруты — на общий API.
	mux := http.NewServeMux()
	mux.Handle("POST /internal/queue/messages", consumer)
	mux.HandleFunc("POST /internal/snapshots/reap", timer.Reap)
	mux.Handle("/", main)
	return mux, nil
}

func breakerThreshold() int {
	if v := os.Getenv("SOURCECRAFT_BREAKER_THRESHOLD"); v != "" {
		if n, err := strconv.Atoi(v); err == nil && n > 0 {
			return n
		}
	}
	return 5
}

func breakerCooldown() time.Duration {
	if v := os.Getenv("SOURCECRAFT_BREAKER_COOLDOWN"); v != "" {
		if d, err := time.ParseDuration(v); err == nil && d > 0 {
			return d
		}
	}
	return 30 * time.Second
}
