package objectstore

import (
	"bytes"
	"context"
	"fmt"
	"io"
	"iter"
	"maps"
	"slices"
	"strings"
	"sync"
	"time"
)

// Memory — Store в памяти для тестов.
type Memory struct {
	mu      sync.Mutex
	objects map[string]memObject
	Now     func() time.Time
}

type memObject struct {
	data []byte
	info ObjectInfo
}

func NewMemory() *Memory {
	return &Memory{objects: map[string]memObject{}, Now: time.Now}
}

func (m *Memory) Put(ctx context.Context, key string, r io.Reader, metadata map[string]string) (ObjectInfo, error) {
	data, err := io.ReadAll(r)
	if err != nil {
		return ObjectInfo{}, fmt.Errorf("objectstore: put %s: %w", key, err)
	}
	info := ObjectInfo{Key: key, Size: int64(len(data)), LastModified: m.Now(), Metadata: maps.Clone(metadata)}
	m.mu.Lock()
	m.objects[key] = memObject{data: data, info: info}
	m.mu.Unlock()
	return info, nil
}

func (m *Memory) Get(ctx context.Context, key string) (io.ReadCloser, ObjectInfo, error) {
	m.mu.Lock()
	defer m.mu.Unlock()
	o, ok := m.objects[key]
	if !ok {
		return nil, ObjectInfo{}, ErrNotFound
	}
	return io.NopCloser(bytes.NewReader(o.data)), o.info, nil
}

func (m *Memory) Stat(ctx context.Context, key string) (ObjectInfo, error) {
	m.mu.Lock()
	defer m.mu.Unlock()
	o, ok := m.objects[key]
	if !ok {
		return ObjectInfo{}, ErrNotFound
	}
	return o.info, nil
}

func (m *Memory) Delete(ctx context.Context, key string) error {
	m.mu.Lock()
	delete(m.objects, key)
	m.mu.Unlock()
	return nil
}

func (m *Memory) List(ctx context.Context, prefix string) iter.Seq2[ObjectInfo, error] {
	m.mu.Lock()
	var infos []ObjectInfo
	for _, k := range slices.Sorted(maps.Keys(m.objects)) {
		if strings.HasPrefix(k, prefix) {
			infos = append(infos, m.objects[k].info)
		}
	}
	m.mu.Unlock()
	return func(yield func(ObjectInfo, error) bool) {
		for _, info := range infos {
			if !yield(info, nil) {
				return
			}
		}
	}
}

// Keys возвращает ключи всех объектов.
func (m *Memory) Keys() []string {
	m.mu.Lock()
	defer m.mu.Unlock()
	return slices.Sorted(maps.Keys(m.objects))
}

// Raw возвращает содержимое объекта.
func (m *Memory) Raw(key string) []byte {
	m.mu.Lock()
	defer m.mu.Unlock()
	return m.objects[key].data
}
