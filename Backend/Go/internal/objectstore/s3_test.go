package objectstore

import (
	"bytes"
	"context"
	"crypto/rand"
	"errors"
	"io"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/johannesboyne/gofakes3"
	"github.com/johannesboyne/gofakes3/backend/s3mem"
)

// Проверяет S3-адаптер против эмулятора S3 (путь запросов minio-go, метаданные, multipart, листинг).
func TestS3AgainstEmulator(t *testing.T) {
	// По TLS minio-go шлёт тело как есть; по plain HTTP — aws-chunked, который эмулятор не декодирует.
	fake := httptest.NewTLSServer(gofakes3.New(s3mem.New()).Server())
	t.Cleanup(fake.Close)

	s, err := NewS3(S3Options{
		Endpoint: fake.URL, Region: "us-east-1", Bucket: "snapshots-test", AccessKey: "k", SecretKey: "s",
		UseSSL: true, Transport: fake.Client().Transport,
	})
	if err != nil {
		t.Fatal(err)
	}
	ctx := context.Background()
	if err := s.EnsureBucket(ctx, "us-east-1", "snapshots", 0); err != nil {
		t.Fatal(err)
	}

	// Больше одной части multipart (16 МБ), длина заранее неизвестна.
	payload := make([]byte, partSize+1<<20)
	_, _ = rand.Read(payload)
	meta := map[string]string{"expires-at": "2026-09-25T18:00:00Z", "branch": "main"}

	if _, err := s.Put(ctx, "snapshots/r1/run1/repo.tar.gz", io.MultiReader(bytes.NewReader(payload)), meta); err != nil {
		t.Fatal(err)
	}

	st, err := s.Stat(ctx, "snapshots/r1/run1/repo.tar.gz")
	if err != nil {
		t.Fatal(err)
	}
	if st.Size != int64(len(payload)) || st.Metadata["expires-at"] != meta["expires-at"] || st.Metadata["branch"] != "main" {
		t.Fatalf("stat = %+v", st)
	}

	body, info, err := s.Get(ctx, "snapshots/r1/run1/repo.tar.gz")
	if err != nil {
		t.Fatal(err)
	}
	got, err := io.ReadAll(body)
	_ = body.Close()
	if err != nil || !bytes.Equal(got, payload) || info.Metadata["branch"] != "main" {
		t.Fatalf("get: err=%v equal=%v meta=%v", err, bytes.Equal(got, payload), info.Metadata)
	}

	var keys []string
	for obj, err := range s.List(ctx, "snapshots/") {
		if err != nil {
			t.Fatal(err)
		}
		keys = append(keys, obj.Key)
	}
	if strings.Join(keys, ",") != "snapshots/r1/run1/repo.tar.gz" {
		t.Fatalf("list = %v", keys)
	}

	if err := s.Delete(ctx, "snapshots/r1/run1/repo.tar.gz"); err != nil {
		t.Fatal(err)
	}
	if _, err := s.Stat(ctx, "snapshots/r1/run1/repo.tar.gz"); !errors.Is(err, ErrNotFound) {
		t.Fatalf("stat after delete: %v", err)
	}
	if _, _, err := s.Get(ctx, "snapshots/r1/run1/repo.tar.gz"); !errors.Is(err, ErrNotFound) {
		t.Fatalf("get after delete: %v", err)
	}
}
