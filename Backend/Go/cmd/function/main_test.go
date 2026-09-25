package main

import "testing"

func TestListenAddr(t *testing.T) {
	t.Setenv("HTTP_ADDR", "")
	t.Setenv("PORT", "")
	if got := listenAddr(); got != ":8080" {
		t.Fatalf("listenAddr() = %q, want :8080", got)
	}

	t.Setenv("PORT", "9000")
	if got := listenAddr(); got != ":9000" {
		t.Fatalf("listenAddr() with PORT = %q, want :9000", got)
	}

	t.Setenv("HTTP_ADDR", ":1234")
	if got := listenAddr(); got != ":1234" {
		t.Fatalf("listenAddr() with HTTP_ADDR = %q, want :1234", got)
	}
}
