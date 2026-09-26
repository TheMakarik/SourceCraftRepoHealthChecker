// Package contract — JSON-контракт с C#-бэкендом.
//
// Типы повторяют record'ы из Backend/CSharp/SourceCraftRepoHealthChecker.Application/SourceCraft/Models.
// Имена полей — camelCase (System.Text.Json web defaults), enum'ы — строками
// (на стороне C# нужен JsonStringEnumConverter).
package contract

import (
	"fmt"
	"strings"
	"time"
)

// DataStatus соответствует Domain.Enums.DataStatus.
type DataStatus string

const (
	Available   DataStatus = "Available"
	NoData      DataStatus = "NoData"
	Unavailable DataStatus = "Unavailable"
)

// Envelope — обёртка любого ответа: статус доступности, requestId и время сбора (раздел 6 ТЗ).
type Envelope struct {
	RequestID     string     `json:"requestId"`
	CollectedAt   time.Time  `json:"collectedAt"`
	Status        DataStatus `json:"status"`
	Data          any        `json:"data"`
	NextPageToken string     `json:"nextPageToken,omitempty"`
	// Reason — почему данных нет или они недоступны; без токенов и содержимого кода.
	Reason string `json:"reason,omitempty"`
}

type Error struct {
	RequestID string `json:"requestId"`
	Code      string `json:"code"`
	Message   string `json:"message"`
}

type SourceCraftUser struct {
	ID          string  `json:"id"`
	Login       string  `json:"login"`
	DisplayName string  `json:"displayName"`
	Email       *string `json:"email"`
}

type SourceCraftRepository struct {
	ID             string    `json:"id"`
	Name           string    `json:"name"`
	FullName       string    `json:"fullName"`
	URL            string    `json:"url"`
	Language       string    `json:"language"`
	LikesCount     int       `json:"likesCount"`
	LastActivityAt time.Time `json:"lastActivityAt"`
	IsPrivate      bool      `json:"isPrivate"`
	DefaultBranch  string    `json:"defaultBranch"`
}

type CommitActivity struct {
	TotalCount    int            `json:"totalCount"`
	FirstCommitAt *time.Time     `json:"firstCommitAt"`
	LastCommitAt  *time.Time     `json:"lastCommitAt"`
	CommitsByDay  map[string]int `json:"commitsByDay"` // ключ — DateOnly "yyyy-MM-dd" (UTC)
}

type Contributor struct {
	Login         string     `json:"login"`
	CommitsCount  int        `json:"commitsCount"`
	IsBot         bool       `json:"isBot"`
	FirstCommitAt *time.Time `json:"firstCommitAt"`
	LastCommitAt  *time.Time `json:"lastCommitAt"`
}

// RepositoryStructure описывает структуру папок репозитория (без корневых файлов README/LICENSE).
type RepositoryStructure struct {
	TotalFiles            int    `json:"totalFiles"`
	TotalDirectories      int    `json:"totalDirectories"`
	MaxDepth              int    `json:"maxDepth"`
	RootFiles             int    `json:"rootFiles"`
	LargestDirectory      string `json:"largestDirectory"`
	LargestDirectoryFiles int    `json:"largestDirectoryFiles"`
}

type ReleaseInfo struct {
	Name        string    `json:"name"`
	Tag         string    `json:"tag"`
	PublishedAt time.Time `json:"publishedAt"`
}

type IssueState string

const (
	IssueOpen   IssueState = "Open"
	IssueClosed IssueState = "Closed"
)

type IssueInfo struct {
	ID              string     `json:"id"`
	Title           string     `json:"title"`
	State           IssueState `json:"state"`
	AuthorLogin     string     `json:"authorLogin"`
	CreatedAt       time.Time  `json:"createdAt"`
	UpdatedAt       time.Time  `json:"updatedAt"`
	ClosedAt        *time.Time `json:"closedAt"`
	FirstResponseAt *time.Time `json:"firstResponseAt"`
}

type MergeRequestState string

const (
	MergeRequestOpen   MergeRequestState = "Open"
	MergeRequestMerged MergeRequestState = "Merged"
	MergeRequestClosed MergeRequestState = "Closed"
)

type MergeRequestInfo struct {
	ID                  string            `json:"id"`
	Title               string            `json:"title"`
	State               MergeRequestState `json:"state"`
	AuthorLogin         string            `json:"authorLogin"`
	CreatedAt           time.Time         `json:"createdAt"`
	MergedAt            *time.Time        `json:"mergedAt"`
	ClosedAt            *time.Time        `json:"closedAt"`
	FirstResponseAt     *time.Time        `json:"firstResponseAt"`
	ReviewCommentsCount int               `json:"reviewCommentsCount"`
}

type PipelineStatus string

const (
	PipelineSuccess  PipelineStatus = "Success"
	PipelineFailed   PipelineStatus = "Failed"
	PipelineRunning  PipelineStatus = "Running"
	PipelineCanceled PipelineStatus = "Canceled"
	PipelineSkipped  PipelineStatus = "Skipped"
)

type PipelineRun struct {
	ID         string         `json:"id"`
	Status     PipelineStatus `json:"status"`
	Branch     string         `json:"branch"`
	StartedAt  time.Time      `json:"startedAt"`
	FinishedAt *time.Time     `json:"finishedAt"`
}

// CodeHealthReport соответствует Application.SourceCraft.Models.CodeHealthReport.
// OldestCommentAge — TimeSpan в формате "c" (как сериализует System.Text.Json), например "12.03:04:05".
type CodeHealthReport struct {
	TodoCount         int     `json:"todoCount"`
	FixmeCount        int     `json:"fixmeCount"`
	TotalCommentCount int     `json:"totalCommentCount"`
	OldestCommentAge  *string `json:"oldestCommentAge"`
}

// DocumentationReport соответствует Application.SourceCraft.Models.DocumentationReport.
type DocumentationReport struct {
	HasReadme                   bool `json:"hasReadme"`
	HasLicense                  bool `json:"hasLicense"`
	HasContributing             bool `json:"hasContributing"`
	HasCodeOwners               bool `json:"hasCodeOwners"`
	HasLocalRunInstructions     bool `json:"hasLocalRunInstructions"`
	HasBuildAndTestInstructions bool `json:"hasBuildAndTestInstructions"`
}

// SecurityFindingKind соответствует Application.SourceCraft.Models.SecurityFindingKind.
type SecurityFindingKind string

const (
	SecurityFindingSast           SecurityFindingKind = "Sast"
	SecurityFindingSca            SecurityFindingKind = "Sca"
	SecurityFindingSecretScanning SecurityFindingKind = "SecretScanning"
)

// SecuritySeverity соответствует Application.SourceCraft.Models.SecuritySeverity.
type SecuritySeverity string

const (
	SecuritySeverityLow      SecuritySeverity = "Low"
	SecuritySeverityMedium   SecuritySeverity = "Medium"
	SecuritySeverityHigh     SecuritySeverity = "High"
	SecuritySeverityCritical SecuritySeverity = "Critical"
)

// SecurityFindingStatus соответствует Application.SourceCraft.Models.SecurityFindingStatus.
type SecurityFindingStatus string

const (
	SecurityFindingOpen  SecurityFindingStatus = "Open"
	SecurityFindingFixed SecurityFindingStatus = "Fixed"
)

// SecurityFinding соответствует Application.SourceCraft.Models.SecurityFinding.
type SecurityFinding struct {
	ID       string                `json:"id"`
	Kind     SecurityFindingKind   `json:"kind"`
	Severity SecuritySeverity      `json:"severity"`
	Status   SecurityFindingStatus `json:"status"`
	Title    string                `json:"title"`
	Package  *string               `json:"package"`
	FilePath *string               `json:"filePath"`
}

// FormatTimeSpan приводит длительность к формату TimeSpan "c" ([d.]hh:mm:ss[.fffffff]),
// который понимает System.Text.Json на стороне C#.
func FormatTimeSpan(d time.Duration) string {
	negative := d < 0
	if negative {
		d = -d
	}
	days := d / (24 * time.Hour)
	d -= days * 24 * time.Hour
	hours := d / time.Hour
	d -= hours * time.Hour
	minutes := d / time.Minute
	d -= minutes * time.Minute
	seconds := d / time.Second
	ticks := int64(d-time.Second*seconds) / 100

	var b strings.Builder
	if negative {
		b.WriteByte('-')
	}
	if days > 0 {
		fmt.Fprintf(&b, "%d.%02d:%02d:%02d", days, hours, minutes, seconds)
	} else {
		fmt.Fprintf(&b, "%02d:%02d:%02d", hours, minutes, seconds)
	}
	if ticks > 0 {
		fmt.Fprintf(&b, ".%07d", ticks)
	}
	return b.String()
}
