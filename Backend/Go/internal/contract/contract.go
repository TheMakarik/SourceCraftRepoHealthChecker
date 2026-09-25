// Package contract — JSON-контракт с C#-бэкендом.
//
// Типы повторяют record'ы из Backend/CSharp/SourceCraftRepoHealthChecker.Application/SourceCraft/Models.
// Имена полей — camelCase (System.Text.Json web defaults), enum'ы — строками
// (на стороне C# нужен JsonStringEnumConverter).
package contract

import "time"

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
	Login        string `json:"login"`
	CommitsCount int    `json:"commitsCount"`
	IsBot        bool   `json:"isBot"`
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
