// Команда server поднимает Go-сервис как обычный HTTP-сервер: локально и в Serverless Container.
package main

import (
	"context"
	"errors"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/appsec"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/config"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/gitrepo"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/httpapi"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/objectstore"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/service"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/snapshot"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/sourcecraft"
	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/yandexid"
)

func main() {
	log := slog.New(slog.NewJSONHandler(os.Stdout, nil))
	if err := run(log); err != nil {
		log.Error("server stopped", "err", err)
		os.Exit(1)
	}
}

func run(log *slog.Logger) error {
	cfg, err := config.Load()
	if err != nil {
		return err
	}

	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()

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
		return err
	}
	initCtx, cancel := context.WithTimeout(ctx, 30*time.Second)
	err = objects.EnsureBucket(initCtx, cfg.Storage.Region, cfg.Storage.Prefix, cfg.Storage.LifecycleDays)
	cancel()
	if err != nil {
		// Хранилище может быть временно недоступно — сервис всё равно поднимается, лёгкие эндпоинты работают.
		log.Warn("object storage init failed", "err", err)
	}

	git := gitrepo.Git{Binary: cfg.Git.Binary}
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
	})
	appSec := appsec.NewClient(appsec.Options{
		BaseURL:    cfg.AppSec.BaseURL,
		Timeout:    cfg.AppSec.Timeout,
		MaxRetries: cfg.AppSec.MaxRetries,
	})
	svc := service.New(api, appSec, snapshots, git, cfg.SourceCraft.GitUsername, cfg.Git.WorkDir, service.Limits{
		MaxItems:           cfg.Service.MaxItems,
		MaxResponseLookups: cfg.Service.MaxResponseLookups,
		Concurrency:        cfg.Service.Concurrency,
	})

	if cfg.ReapInterval > 0 {
		go reapLoop(ctx, svc, cfg.ReapInterval, log)
	}

	srv := &http.Server{
		Addr: cfg.HTTPAddr,
		Handler: httpapi.New(svc, httpapi.Options{
			ServiceToken:  cfg.SourceCraft.ServiceToken,
			InternalToken: cfg.InternalToken,
			YandexID: yandexid.New(yandexid.Options{
				ClientID:     cfg.YandexID.ClientID,
				ClientSecret: cfg.YandexID.ClientSecret,
				RedirectURI:  cfg.YandexID.RedirectURI,
				Scope:        cfg.YandexID.Scope,
			}),
		}, log),
		ReadHeaderTimeout: 10 * time.Second,
		ReadTimeout:       30 * time.Second,
		// WriteTimeout не ставим: создание снапшота крупного репозитория ограничено GIT_CLONE_TIMEOUT.
		IdleTimeout: 120 * time.Second,
	}

	errCh := make(chan error, 1)
	go func() {
		log.Info("listening", "addr", cfg.HTTPAddr)
		errCh <- srv.ListenAndServe()
	}()

	select {
	case err := <-errCh:
		return err
	case <-ctx.Done():
	}

	shutdownCtx, cancel := context.WithTimeout(context.Background(), 30*time.Second)
	defer cancel()
	if err := srv.Shutdown(shutdownCtx); err != nil && !errors.Is(err, http.ErrServerClosed) {
		return err
	}
	return nil
}

func reapLoop(ctx context.Context, svc *service.Service, every time.Duration, log *slog.Logger) {
	t := time.NewTicker(every)
	defer t.Stop()
	for {
		select {
		case <-ctx.Done():
			return
		case <-t.C:
			n, err := svc.ReapSnapshots(ctx)
			if err != nil {
				log.Warn("reap snapshots", "deleted", n, "err", err)
			} else if n > 0 {
				log.Info("reaped expired snapshots", "deleted", n)
			}
		}
	}
}
