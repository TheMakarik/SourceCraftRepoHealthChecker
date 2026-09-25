// Package objectstore — минимальная абстракция над S3-совместимым хранилищем.
package objectstore

import (
	"context"
	"errors"
	"io"
	"iter"
	"time"
)

var ErrNotFound = errors.New("objectstore: object not found")

type ObjectInfo struct {
	Key          string
	Size         int64
	LastModified time.Time
	// Metadata — пользовательские метаданные (x-amz-meta-*), ключи в нижнем регистре.
	Metadata map[string]string
}

type Store interface {
	// Put загружает поток неизвестной длины.
	Put(ctx context.Context, key string, r io.Reader, metadata map[string]string) (ObjectInfo, error)
	Get(ctx context.Context, key string) (io.ReadCloser, ObjectInfo, error)
	Stat(ctx context.Context, key string) (ObjectInfo, error)
	Delete(ctx context.Context, key string) error
	// List перечисляет объекты с префиксом вместе с метаданными.
	List(ctx context.Context, prefix string) iter.Seq2[ObjectInfo, error]
}
