package config

import (
	"strings"
	"testing"
	"time"
)

var allEnvKeys = []string{
	"HTTP_ADDR", "INTERNAL_TOKEN", "SNAPSHOT_REAP_INTERVAL",
	"SOURCECRAFT_API_URL", "SOURCECRAFT_TOKEN", "SOURCECRAFT_GIT_USERNAME",
	"SOURCECRAFT_TIMEOUT", "SOURCECRAFT_MAX_RETRIES", "SOURCECRAFT_RPS",
	"APPSEC_API_URL", "APPSEC_TIMEOUT", "APPSEC_MAX_RETRIES",
	"SERVICECRAFT_MAX_ITEMS", "SERVICECRAFT_MAX_RESPONSE_LOOKUPS", "SERVICECRAFT_CONCURRENCY",
	"YANDEX_CLIENT_ID", "YANDEX_CLIENT_SECRET", "YANDEX_REDIRECT_URI", "YANDEX_SCOPE",
	"S3_ENDPOINT", "S3_REGION", "S3_BUCKET", "S3_ACCESS_KEY", "S3_SECRET_KEY",
	"S3_USE_SSL", "S3_SSE", "S3_PREFIX", "SNAPSHOT_TTL", "S3_LIFECYCLE_DAYS",
	"GIT_BINARY", "GIT_WORKDIR", "GIT_CLONE_TIMEOUT", "GIT_MAX_REPO_MB",
}

func clearEnv(t *testing.T) {
	t.Helper()
	for _, key := range allEnvKeys {
		t.Setenv(key, "")
	}
}

func TestLoadDefaults(t *testing.T) {
	clearEnv(t)
	t.Setenv("S3_ENDPOINT", "s3.example")
	t.Setenv("S3_BUCKET", "bucket")

	cfg, err := Load()
	if err != nil {
		t.Fatalf("Load() error = %v", err)
	}

	if cfg.HTTPAddr != ":8080" || cfg.InternalToken != "" || cfg.ReapInterval != 0 {
		t.Errorf("top-level defaults = %+v", cfg)
	}
	if cfg.SourceCraft.BaseURL != "https://api.sourcecraft.tech" {
		t.Errorf("BaseURL = %q", cfg.SourceCraft.BaseURL)
	}
	if cfg.SourceCraft.Timeout != 15*time.Second || cfg.SourceCraft.MaxRetries != 3 || cfg.SourceCraft.RequestsPerSec != 10 {
		t.Errorf("SourceCraft defaults = %+v", cfg.SourceCraft)
	}
	if cfg.AppSec.BaseURL != "https://appsec.sourcecraft.tech" || cfg.AppSec.Timeout != 15*time.Second || cfg.AppSec.MaxRetries != 3 {
		t.Errorf("AppSec defaults = %+v", cfg.AppSec)
	}
	if cfg.Service.MaxItems != 500 || cfg.Service.MaxResponseLookups != 100 || cfg.Service.Concurrency != 4 {
		t.Errorf("Service defaults = %+v", cfg.Service)
	}
	if cfg.Storage.Region != "ru-central1" || !cfg.Storage.UseSSL || !cfg.Storage.Encrypt {
		t.Errorf("Storage defaults = %+v", cfg.Storage)
	}
	if cfg.Storage.Prefix != "snapshots" || cfg.Storage.SnapshotTTL != 3*time.Hour || cfg.Storage.LifecycleDays != 1 {
		t.Errorf("Storage defaults = %+v", cfg.Storage)
	}
	if cfg.Git.Binary != "git" || cfg.Git.CloneTimeout != 15*time.Minute || cfg.Git.MaxRepoBytes != int64(2048)<<20 {
		t.Errorf("Git defaults = %+v", cfg.Git)
	}
}

func TestLoadOverrides(t *testing.T) {
	clearEnv(t)
	t.Setenv("HTTP_ADDR", ":9090")
	t.Setenv("INTERNAL_TOKEN", "secret")
	t.Setenv("SNAPSHOT_REAP_INTERVAL", "45s")
	t.Setenv("SOURCECRAFT_API_URL", "https://example.com/")
	t.Setenv("SOURCECRAFT_TIMEOUT", "5s")
	t.Setenv("SOURCECRAFT_MAX_RETRIES", "7")
	t.Setenv("SOURCECRAFT_RPS", "2.5")
	t.Setenv("APPSEC_API_URL", "https://appsec.example/")
	t.Setenv("APPSEC_TIMEOUT", "4s")
	t.Setenv("APPSEC_MAX_RETRIES", "2")
	t.Setenv("SERVICECRAFT_MAX_ITEMS", "10")
	t.Setenv("SERVICECRAFT_MAX_RESPONSE_LOOKUPS", "11")
	t.Setenv("SERVICECRAFT_CONCURRENCY", "12")
	t.Setenv("S3_ENDPOINT", "s3.example")
	t.Setenv("S3_REGION", "us-east-1")
	t.Setenv("S3_BUCKET", "bucket")
	t.Setenv("S3_USE_SSL", "false")
	t.Setenv("S3_SSE", "0")
	t.Setenv("S3_PREFIX", "/custom/")
	t.Setenv("SNAPSHOT_TTL", "1h")
	t.Setenv("S3_LIFECYCLE_DAYS", "7")
	t.Setenv("GIT_BINARY", "/usr/bin/git")
	t.Setenv("GIT_CLONE_TIMEOUT", "2m")
	t.Setenv("GIT_MAX_REPO_MB", "10")

	cfg, err := Load()
	if err != nil {
		t.Fatalf("Load() error = %v", err)
	}

	if cfg.HTTPAddr != ":9090" || cfg.InternalToken != "secret" || cfg.ReapInterval != 45*time.Second {
		t.Errorf("top-level = %+v", cfg)
	}
	if cfg.SourceCraft.BaseURL != "https://example.com" {
		t.Errorf("BaseURL = %q", cfg.SourceCraft.BaseURL)
	}
	if cfg.SourceCraft.Timeout != 5*time.Second || cfg.SourceCraft.MaxRetries != 7 || cfg.SourceCraft.RequestsPerSec != 2.5 {
		t.Errorf("SourceCraft = %+v", cfg.SourceCraft)
	}
	if cfg.AppSec.BaseURL != "https://appsec.example" || cfg.AppSec.Timeout != 4*time.Second || cfg.AppSec.MaxRetries != 2 {
		t.Errorf("AppSec = %+v", cfg.AppSec)
	}
	if cfg.Service.MaxItems != 10 || cfg.Service.MaxResponseLookups != 11 || cfg.Service.Concurrency != 12 {
		t.Errorf("Service = %+v", cfg.Service)
	}
	if cfg.Storage.Region != "us-east-1" || cfg.Storage.UseSSL || cfg.Storage.Encrypt {
		t.Errorf("Storage = %+v", cfg.Storage)
	}
	if cfg.Storage.Prefix != "custom" || cfg.Storage.SnapshotTTL != time.Hour || cfg.Storage.LifecycleDays != 7 {
		t.Errorf("Storage = %+v", cfg.Storage)
	}
	if cfg.Git.Binary != "/usr/bin/git" || cfg.Git.CloneTimeout != 2*time.Minute || cfg.Git.MaxRepoBytes != int64(10)<<20 {
		t.Errorf("Git = %+v", cfg.Git)
	}
}

func TestLoadAggregatesErrors(t *testing.T) {
	clearEnv(t)
	t.Setenv("S3_ENDPOINT", "s3.example")
	t.Setenv("S3_BUCKET", "bucket")
	bad := map[string]string{
		"SNAPSHOT_REAP_INTERVAL":   "soon",
		"SOURCECRAFT_TIMEOUT":      "later",
		"SOURCECRAFT_MAX_RETRIES":  "many",
		"SOURCECRAFT_RPS":          "fast",
		"APPSEC_TIMEOUT":           "soon",
		"APPSEC_MAX_RETRIES":       "many",
		"SERVICECRAFT_MAX_ITEMS":   "all",
		"SERVICECRAFT_CONCURRENCY": "lots",
		"S3_USE_SSL":               "maybe",
		"SNAPSHOT_TTL":             "forever",
		"S3_LIFECYCLE_DAYS":        "never",
		"GIT_CLONE_TIMEOUT":        "eventually",
		"GIT_MAX_REPO_MB":          "huge",
	}
	for key, value := range bad {
		t.Setenv(key, value)
	}

	cfg, err := Load()
	if err == nil {
		t.Fatal("Load() error = nil, want aggregated error")
	}
	for key := range bad {
		if !strings.Contains(err.Error(), key) {
			t.Errorf("error %q does not mention %s", err, key)
		}
	}

	if cfg.SourceCraft.Timeout != 15*time.Second || cfg.SourceCraft.MaxRetries != 3 || cfg.SourceCraft.RequestsPerSec != 10 {
		t.Errorf("SourceCraft fallbacks = %+v", cfg.SourceCraft)
	}
	if cfg.AppSec.Timeout != 15*time.Second || cfg.AppSec.MaxRetries != 3 {
		t.Errorf("AppSec fallbacks = %+v", cfg.AppSec)
	}
	if cfg.Service.MaxItems != 500 || cfg.Service.Concurrency != 4 {
		t.Errorf("Service fallbacks = %+v", cfg.Service)
	}
	if !cfg.Storage.UseSSL || cfg.Storage.SnapshotTTL != 3*time.Hour || cfg.Storage.LifecycleDays != 1 {
		t.Errorf("Storage fallbacks = %+v", cfg.Storage)
	}
	if cfg.Git.CloneTimeout != 15*time.Minute || cfg.Git.MaxRepoBytes != int64(2048)<<20 {
		t.Errorf("Git fallbacks = %+v", cfg.Git)
	}
}

func TestLoadRequiresStorage(t *testing.T) {
	clearEnv(t)

	_, err := Load()
	if err == nil || !strings.Contains(err.Error(), "S3_ENDPOINT") || !strings.Contains(err.Error(), "S3_BUCKET") {
		t.Fatalf("both missing: error = %v", err)
	}

	t.Setenv("S3_ENDPOINT", "s3.example")
	_, err = Load()
	if err == nil || strings.Contains(err.Error(), "S3_ENDPOINT") || !strings.Contains(err.Error(), "S3_BUCKET") {
		t.Fatalf("bucket missing: error = %v", err)
	}

	clearEnv(t)
	t.Setenv("S3_BUCKET", "bucket")
	_, err = Load()
	if err == nil || !strings.Contains(err.Error(), "S3_ENDPOINT") || strings.Contains(err.Error(), "S3_BUCKET") {
		t.Fatalf("endpoint missing: error = %v", err)
	}
}
