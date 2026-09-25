package objectstore

import (
	"context"
	"errors"
	"fmt"
	"io"
	"iter"
	"net/http"
	"strings"

	"github.com/minio/minio-go/v7"
	"github.com/minio/minio-go/v7/pkg/credentials"
	"github.com/minio/minio-go/v7/pkg/encrypt"
	"github.com/minio/minio-go/v7/pkg/lifecycle"
)

// partSize ограничивает память на загрузку потока неизвестной длины (по умолчанию minio буферизует 512 МБ).
const partSize = 16 << 20

type S3Options struct {
	Endpoint  string
	Region    string
	Bucket    string
	AccessKey string
	SecretKey string
	UseSSL    bool
	// Encrypt включает SSE-S3 (AES256) на стороне хранилища.
	Encrypt bool
	// Transport — необязательный HTTP-транспорт (например, со своим CA).
	Transport http.RoundTripper
}

// S3 работает с любым S3-совместимым хранилищем: Yandex Object Storage, MinIO, AWS S3.
type S3 struct {
	client  *minio.Client
	bucket  string
	encrypt bool
}

func NewS3(opts S3Options) (*S3, error) {
	endpoint := strings.TrimPrefix(strings.TrimPrefix(opts.Endpoint, "https://"), "http://")
	client, err := minio.New(endpoint, &minio.Options{
		Creds:     credentials.NewStaticV4(opts.AccessKey, opts.SecretKey, ""),
		Secure:    opts.UseSSL,
		Region:    opts.Region,
		Transport: opts.Transport,
	})
	if err != nil {
		return nil, fmt.Errorf("objectstore: %w", err)
	}
	return &S3{client: client, bucket: opts.Bucket, encrypt: opts.Encrypt}, nil
}

// EnsureBucket создаёт бакет при отсутствии и ставит lifecycle-правило: объекты с prefix
// удаляются через expireDays дней. Это страховка на случай, если явное удаление и reaper не сработали.
func (s *S3) EnsureBucket(ctx context.Context, region, prefix string, expireDays int) error {
	exists, err := s.client.BucketExists(ctx, s.bucket)
	if err != nil {
		return fmt.Errorf("objectstore: bucket exists: %w", err)
	}
	if !exists {
		if err := s.client.MakeBucket(ctx, s.bucket, minio.MakeBucketOptions{Region: region}); err != nil {
			return fmt.Errorf("objectstore: make bucket: %w", err)
		}
	}
	if expireDays <= 0 {
		return nil
	}
	cfg := lifecycle.NewConfiguration()
	cfg.Rules = []lifecycle.Rule{{
		ID:         "expire-repo-snapshots",
		Status:     "Enabled",
		RuleFilter: lifecycle.Filter{Prefix: strings.TrimSuffix(prefix, "/") + "/"},
		Expiration: lifecycle.Expiration{Days: lifecycle.ExpirationDays(expireDays)},
		AbortIncompleteMultipartUpload: lifecycle.AbortIncompleteMultipartUpload{
			DaysAfterInitiation: lifecycle.ExpirationDays(1),
		},
	}}
	if err := s.client.SetBucketLifecycle(ctx, s.bucket, cfg); err != nil {
		return fmt.Errorf("objectstore: set lifecycle: %w", err)
	}
	return nil
}

func (s *S3) Put(ctx context.Context, key string, r io.Reader, metadata map[string]string) (ObjectInfo, error) {
	opts := minio.PutObjectOptions{
		ContentType:  "application/gzip",
		UserMetadata: metadata,
		PartSize:     partSize,
	}
	if s.encrypt {
		opts.ServerSideEncryption = encrypt.NewSSE()
	}
	info, err := s.client.PutObject(ctx, s.bucket, key, r, -1, opts)
	if err != nil {
		return ObjectInfo{}, fmt.Errorf("objectstore: put %s: %w", key, err)
	}
	return ObjectInfo{Key: key, Size: info.Size, LastModified: info.LastModified, Metadata: metadata}, nil
}

func (s *S3) Get(ctx context.Context, key string) (io.ReadCloser, ObjectInfo, error) {
	obj, err := s.client.GetObject(ctx, s.bucket, key, minio.GetObjectOptions{})
	if err != nil {
		return nil, ObjectInfo{}, s.wrap("get", key, err)
	}
	st, err := obj.Stat()
	if err != nil {
		_ = obj.Close()
		return nil, ObjectInfo{}, s.wrap("get", key, err)
	}
	return obj, toInfo(st), nil
}

func (s *S3) Stat(ctx context.Context, key string) (ObjectInfo, error) {
	st, err := s.client.StatObject(ctx, s.bucket, key, minio.StatObjectOptions{})
	if err != nil {
		return ObjectInfo{}, s.wrap("stat", key, err)
	}
	return toInfo(st), nil
}

func (s *S3) Delete(ctx context.Context, key string) error {
	if err := s.client.RemoveObject(ctx, s.bucket, key, minio.RemoveObjectOptions{}); err != nil {
		return s.wrap("delete", key, err)
	}
	return nil
}

func (s *S3) List(ctx context.Context, prefix string) iter.Seq2[ObjectInfo, error] {
	return func(yield func(ObjectInfo, error) bool) {
		ctx, cancel := context.WithCancel(ctx)
		defer cancel()
		for obj := range s.client.ListObjects(ctx, s.bucket, minio.ListObjectsOptions{Prefix: prefix, Recursive: true, WithMetadata: true}) {
			if obj.Err != nil {
				yield(ObjectInfo{}, s.wrap("list", prefix, obj.Err))
				return
			}
			if !yield(toInfo(obj), nil) {
				return
			}
		}
	}
}

func (s *S3) wrap(op, key string, err error) error {
	var resp minio.ErrorResponse
	if errors.As(err, &resp) && (resp.StatusCode == http.StatusNotFound || resp.Code == "NoSuchKey") {
		return fmt.Errorf("objectstore: %s %s: %w", op, key, ErrNotFound)
	}
	return fmt.Errorf("objectstore: %s %s: %w", op, key, err)
}

func toInfo(o minio.ObjectInfo) ObjectInfo {
	meta := make(map[string]string, len(o.UserMetadata))
	for k, v := range o.UserMetadata {
		// ListObjects отдаёт ключи как X-Amz-Meta-Foo, StatObject — как Foo.
		k = strings.ToLower(strings.TrimPrefix(strings.ToLower(k), "x-amz-meta-"))
		meta[k] = v
	}
	return ObjectInfo{Key: o.Key, Size: o.Size, LastModified: o.LastModified, Metadata: meta}
}
