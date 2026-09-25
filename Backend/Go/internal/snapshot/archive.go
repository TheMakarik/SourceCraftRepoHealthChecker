package snapshot

import (
	"archive/tar"
	"compress/gzip"
	"errors"
	"fmt"
	"io"
	"io/fs"
	"os"
	"path/filepath"
	"strings"
)

// writeArchive потоково пакует каталог dir в tar.gz. Симлинки и спецфайлы пропускаются:
// в bare-репозитории они не нужны, а при распаковке опасны.
func writeArchive(w io.Writer, dir string) error {
	gz, err := gzip.NewWriterLevel(w, gzip.BestSpeed)
	if err != nil {
		return err
	}
	tw := tar.NewWriter(gz)

	err = filepath.WalkDir(dir, func(path string, d fs.DirEntry, err error) error {
		if err != nil {
			return err
		}
		rel, err := filepath.Rel(dir, path)
		if err != nil || rel == "." {
			return err
		}
		if !d.IsDir() && !d.Type().IsRegular() {
			return nil
		}
		info, err := d.Info()
		if err != nil {
			return err
		}
		hdr, err := tar.FileInfoHeader(info, "")
		if err != nil {
			return err
		}
		hdr.Name = filepath.ToSlash(rel)
		hdr.Uid, hdr.Gid, hdr.Uname, hdr.Gname = 0, 0, "", ""
		if err := tw.WriteHeader(hdr); err != nil {
			return err
		}
		if d.IsDir() {
			return nil
		}
		f, err := os.Open(path)
		if err != nil {
			return err
		}
		defer f.Close()
		_, err = io.Copy(tw, f)
		return err
	})
	if err != nil {
		return err
	}
	if err := tw.Close(); err != nil {
		return err
	}
	return gz.Close()
}

var errArchiveTooLarge = errors.New("snapshot: archive exceeds size limit")

// extractArchive распаковывает tar.gz в dir, отвергая пути вне dir и превышение maxBytes.
func extractArchive(r io.Reader, dir string, maxBytes int64) error {
	gz, err := gzip.NewReader(r)
	if err != nil {
		return fmt.Errorf("snapshot: open gzip: %w", err)
	}
	defer gz.Close()
	tr := tar.NewReader(gz)

	root, err := os.OpenRoot(dir)
	if err != nil {
		return err
	}
	defer root.Close()

	var total int64
	for {
		hdr, err := tr.Next()
		if errors.Is(err, io.EOF) {
			return nil
		}
		if err != nil {
			return fmt.Errorf("snapshot: read tar: %w", err)
		}
		name := filepath.FromSlash(hdr.Name)
		if !filepath.IsLocal(name) || strings.Contains(hdr.Name, "\\") {
			return fmt.Errorf("snapshot: unsafe path in archive: %q", hdr.Name)
		}

		switch hdr.Typeflag {
		case tar.TypeDir:
			if err := root.MkdirAll(name, 0o755); err != nil {
				return err
			}
		case tar.TypeReg:
			total += hdr.Size
			if maxBytes > 0 && total > maxBytes {
				return errArchiveTooLarge
			}
			if err := root.MkdirAll(filepath.Dir(name), 0o755); err != nil {
				return err
			}
			f, err := root.OpenFile(name, os.O_CREATE|os.O_WRONLY|os.O_TRUNC, 0o644)
			if err != nil {
				return err
			}
			_, err = io.Copy(f, io.LimitReader(tr, hdr.Size))
			if cerr := f.Close(); err == nil {
				err = cerr
			}
			if err != nil {
				return err
			}
		default:
			// Прочие типы записей (симлинки, устройства) игнорируются.
		}
	}
}
