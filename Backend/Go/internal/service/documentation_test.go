package service

import (
	"encoding/json"
	"testing"
	"time"

	"github.com/TheMakarik/SourceCraftRepoHealthChecker/Backend/Go/internal/contract"
)

func TestClassifyDocFile(t *testing.T) {
	cases := map[string]docKind{
		"README.md":               docReadme,
		"readme":                  docReadme,
		"docs/README.rst":         docReadme,
		"src/README.md":           docOther,
		"LICENSE":                 docLicense,
		"LICENSE.txt":             docLicense,
		"COPYING":                 docLicense,
		"docs/LICENSE":            docOther,
		"CONTRIBUTING.md":         docContributing,
		".github/CONTRIBUTING":    docContributing,
		"CODEOWNERS":              docCodeOwners,
		".sourcecraft/CODEOWNERS": docCodeOwners,
		"lib/CODEOWNERS":          docOther,
		"main.go":                 docOther,
	}
	for p, want := range cases {
		if got := classifyDocFile(p); got != want {
			t.Errorf("%s: got %d, want %d", p, got, want)
		}
	}
}

func TestDetectInstructions(t *testing.T) {
	cases := []struct {
		name              string
		md                string
		run, buildAndTest bool
	}{
		{"empty", "# Project\n\nJust text.", false, false},
		{"words without code don't count", "# Project\n\nRun it, build it and test it.\n## Testing\nWe love tests.", false, false},
		{"run command in code block", "# App\n```bash\ndocker compose up --build\n```", true, false},
		{"russian run heading with code", "## Запуск\n```\n./app\n```", true, false},
		{"build and test commands", "```sh\n$ go build ./...\n$ go test ./...\n```", false, true},
		{"build only is not enough", "```\ndotnet build\n```", false, false},
		{"full readme", "# Svc\n## Quick start\n```\nnpm install\nnpm start\n```\n## Tests\n```\nnpm test\n```", true, true},
		{"setext headings", "Сборка\n======\n```\nmake\n```\nТесты\n-----\n```\npytest\n```", false, true},
	}
	for _, tc := range cases {
		run, bt := detectInstructions(tc.md)
		if run != tc.run || bt != tc.buildAndTest {
			t.Errorf("%s: run=%v buildAndTest=%v, want %v %v", tc.name, run, bt, tc.run, tc.buildAndTest)
		}
	}
}

func TestSampleEvenly(t *testing.T) {
	got := sampleEvenly([]int{9, 1, 5, 3, 7}, 3)
	if len(got) != 3 || got[0] != 1 || got[1] != 5 || got[2] != 9 {
		t.Fatalf("got %v", got)
	}
}

func TestDurationJSON(t *testing.T) {
	cases := map[time.Duration]string{
		90 * time.Second: `"00:01:30"`,
		400*24*time.Hour + 3*time.Hour + 1500*time.Millisecond: `"400.03:00:01"`,
	}
	for d, want := range cases {
		got, _ := json.Marshal(contract.Duration(d))
		if string(got) != want {
			t.Errorf("%v: got %s, want %s", d, got, want)
		}
	}
}
