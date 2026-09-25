package sourcecraft

import (
	"strconv"
	"time"
)

// Типы повторяют схемы из https://api.sourcecraft.tech/docs/sourcecraft.swagger.json.
// uint64-поля SourceCraft отдаёт строками — для них Uint64String.

type Uint64String string

func (s Uint64String) Int() int {
	n, _ := strconv.ParseUint(string(s), 10, 64)
	return int(n)
}

type UserEmbedded struct {
	ID   string `json:"id"`
	Slug string `json:"slug"`
}

// ExternalUser — значение id/slug у внешних (не SourceCraft) пользователей.
const ExternalUser = "<external-user>"

type PullRequestEmbedded struct {
	ID   string `json:"id"`
	Slug string `json:"slug"`
}

type OrganizationEmbedded struct {
	ID   string `json:"id"`
	Slug string `json:"slug"`
}

type Repository struct {
	ID            string               `json:"id"`
	Name          string               `json:"name"`
	Slug          string               `json:"slug"`
	DefaultBranch string               `json:"default_branch"`
	Organization  OrganizationEmbedded `json:"organization"`
	IsEmpty       bool                 `json:"is_empty"`
	Description   string               `json:"description"`
	Visibility    string               `json:"visibility"` // public | internal | private
	CloneURL      struct {
		HTTPS string `json:"https"`
		SSH   string `json:"ssh"`
	} `json:"clone_url"`
	WebURL   string `json:"web_url"`
	Counters struct {
		Forks        Uint64String `json:"forks"`
		PullRequests Uint64String `json:"pull_requests"`
		Issues       Uint64String `json:"issues"`
		Tags         Uint64String `json:"tags"`
		Branches     Uint64String `json:"branches"`
	} `json:"counters"`
	LastUpdated time.Time `json:"last_updated"`
	Language    struct {
		Name string `json:"name"`
	} `json:"language"`
	Rating struct {
		Value          float64 `json:"value"`
		Percentile     float64 `json:"percentile"`
		ReactionCounts []struct {
			Type  string       `json:"type"` // positive_low (Like) | positive_medium (Heart) | positive_high (Diamond)
			Count Uint64String `json:"count"`
		} `json:"reaction_counts"`
	} `json:"rating"`
}

type UserProfile struct {
	ID          string `json:"id"`
	DisplayName string `json:"display_name"`
	Username    string `json:"username"`
}

type IssueStatus struct {
	ID         string `json:"id"`
	Slug       string `json:"slug"`
	Name       string `json:"name"`
	StatusType string `json:"status_type"` // initial | in_progress | paused | completed | cancelled
}

type Issue struct {
	ID          string       `json:"id"`
	Slug        string       `json:"slug"`
	Title       string       `json:"title"`
	Status      IssueStatus  `json:"status"`
	Author      UserEmbedded `json:"author"`
	CreatedAt   time.Time    `json:"created_at"`
	UpdatedAt   time.Time    `json:"updated_at"`
	CompletedAt *time.Time   `json:"completed_at"`
}

type IssueComment struct {
	ID        string       `json:"id"`
	Author    UserEmbedded `json:"author"`
	CreatedAt time.Time    `json:"created_at"`
}

type PullRequest struct {
	ID           string       `json:"id"`
	Slug         string       `json:"slug"`
	Title        string       `json:"title"`
	Author       UserEmbedded `json:"author"`
	Status       string       `json:"status"` // draft | open | discarded | merging | merged
	SourceBranch string       `json:"source_branch"`
	TargetBranch string       `json:"target_branch"`
	CreatedAt    time.Time    `json:"created_at"`
	UpdatedAt    time.Time    `json:"updated_at"`
}

type PullRequestComment struct {
	ID          string       `json:"id"`
	Author      UserEmbedded `json:"author"`
	CreatedAt   time.Time    `json:"created_at"`
	IsDeleted   bool         `json:"is_deleted"`
	IsPublished bool         `json:"is_published"`
}

type Release struct {
	ID         string     `json:"id"`
	Tag        string     `json:"tag"`
	Title      string     `json:"title"`
	Status     string     `json:"status"` // draft | published | discarded
	CreatedAt  time.Time  `json:"created_at"`
	ReleasedAt *time.Time `json:"released_at"`
}

type Run struct {
	ID     string `json:"id"`
	Slug   string `json:"slug"`
	Status string `json:"status"` // created | prepared | processing | success | failed | canceled | timeout | skipped | awaiting_approval | rejected
	Dates  struct {
		CreatedAt  *time.Time `json:"created_at"`
		StartedAt  *time.Time `json:"started_at"`
		FinishedAt *time.Time `json:"finished_at"`
	} `json:"dates"`
	EventType string               `json:"event_type"`
	Pull      *PullRequestEmbedded `json:"pull"`
}

type TreeEntry struct {
	Name string `json:"name"`
	Path string `json:"path"`
	Type string `json:"type"` // file | executable | dir | symlink | submodule
}
