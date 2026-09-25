package sourcecraft

import (
	"context"
	"net/url"
	"strconv"
)

// Все методы принимают token — PAT/OAuth-токен того, от чьего имени идёт запрос.
// Так приватные данные читаются только в рамках прав пользователя.

func (c *Client) CurrentUser(ctx context.Context, token string) (UserProfile, error) {
	var u UserProfile
	err := c.get(ctx, token, "/user", nil, &u)
	return u, err
}

func (c *Client) Repository(ctx context.Context, token, repoID string) (Repository, error) {
	var r Repository
	err := c.get(ctx, token, "/repos/id:"+url.PathEscape(repoID), nil, &r)
	return r, err
}

// DiscoverRepositories — одна страница публичного каталога (GET /repos), по умолчанию по убыванию рейтинга.
func (c *Client) DiscoverRepositories(ctx context.Context, token, pageToken string, pageSize int, sortBy string) (Page[Repository], error) {
	q := url.Values{}
	if pageSize > 0 {
		q.Set("page_size", strconv.Itoa(min(pageSize, maxPageSize)))
	}
	if pageToken != "" {
		q.Set("page_token", pageToken)
	}
	if sortBy != "" {
		q.Set("sort_by", sortBy)
	}
	var resp struct {
		Repositories  []Repository `json:"repositories"`
		NextPageToken string       `json:"next_page_token"`
	}
	err := c.get(ctx, token, "/repos", q, &resp)
	return Page[Repository]{Items: resp.Repositories, NextPageToken: resp.NextPageToken}, err
}

func (c *Client) MyRepositories(ctx context.Context, token string) ([]Repository, error) {
	return listAll(ctx, 0, func(ctx context.Context, pt string) (Page[Repository], error) {
		var resp struct {
			Repositories  []Repository `json:"repositories"`
			NextPageToken string       `json:"next_page_token"`
		}
		err := c.get(ctx, token, "/me/repos", pageQuery(pt, nil), &resp)
		return Page[Repository]{Items: resp.Repositories, NextPageToken: resp.NextPageToken}, err
	})
}

func (c *Client) Issues(ctx context.Context, token, repoID string, limit int) ([]Issue, error) {
	return listAll(ctx, limit, func(ctx context.Context, pt string) (Page[Issue], error) {
		var resp struct {
			Issues        []Issue `json:"issues"`
			NextPageToken string  `json:"next_page_token"`
		}
		err := c.get(ctx, token, "/repos/id:"+url.PathEscape(repoID)+"/issues", pageQuery(pt, url.Values{"sort_by": {"-created_at"}}), &resp)
		return Page[Issue]{Items: resp.Issues, NextPageToken: resp.NextPageToken}, err
	})
}

// IssueComments — первые комментарии issue в хронологическом порядке (для времени первого ответа).
func (c *Client) IssueComments(ctx context.Context, token, issueID string, limit int) ([]IssueComment, error) {
	return listAll(ctx, limit, func(ctx context.Context, pt string) (Page[IssueComment], error) {
		var resp struct {
			Comments      []IssueComment `json:"issue_comments"`
			NextPageToken string         `json:"next_page_token"`
		}
		err := c.get(ctx, token, "/issues/id:"+url.PathEscape(issueID)+"/comments", pageQuery(pt, url.Values{"sort_by": {"created_at"}}), &resp)
		return Page[IssueComment]{Items: resp.Comments, NextPageToken: resp.NextPageToken}, err
	})
}

func (c *Client) PullRequests(ctx context.Context, token, repoID string, limit int) ([]PullRequest, error) {
	return listAll(ctx, limit, func(ctx context.Context, pt string) (Page[PullRequest], error) {
		var resp struct {
			PullRequests  []PullRequest `json:"pull_requests"`
			NextPageToken string        `json:"next_page_token"`
		}
		err := c.get(ctx, token, "/repos/id:"+url.PathEscape(repoID)+"/pulls", pageQuery(pt, url.Values{"sort_by": {"-created_at"}}), &resp)
		return Page[PullRequest]{Items: resp.PullRequests, NextPageToken: resp.NextPageToken}, err
	})
}

func (c *Client) PullRequestComments(ctx context.Context, token, prID string, limit int) ([]PullRequestComment, error) {
	return listAll(ctx, limit, func(ctx context.Context, pt string) (Page[PullRequestComment], error) {
		var resp struct {
			Comments      []PullRequestComment `json:"pull_request_comments"`
			NextPageToken string               `json:"next_page_token"`
		}
		err := c.get(ctx, token, "/pulls/id:"+url.PathEscape(prID)+"/comments", pageQuery(pt, url.Values{"sort_by": {"created_at"}}), &resp)
		return Page[PullRequestComment]{Items: resp.Comments, NextPageToken: resp.NextPageToken}, err
	})
}

func (c *Client) Releases(ctx context.Context, token, repoID string) ([]Release, error) {
	return listAll(ctx, 0, func(ctx context.Context, pt string) (Page[Release], error) {
		var resp struct {
			Releases      []Release `json:"releases"`
			NextPageToken string    `json:"next_page_token"`
		}
		err := c.get(ctx, token, "/repos/id:"+url.PathEscape(repoID)+"/releases", pageQuery(pt, nil), &resp)
		return Page[Release]{Items: resp.Releases, NextPageToken: resp.NextPageToken}, err
	})
}

func (c *Client) Runs(ctx context.Context, token, repoID string, limit int) ([]Run, error) {
	return listAll(ctx, limit, func(ctx context.Context, pt string) (Page[Run], error) {
		var resp struct {
			Runs          []Run  `json:"runs"`
			NextPageToken string `json:"next_page_token"`
		}
		err := c.get(ctx, token, "/repos/id:"+url.PathEscape(repoID)+"/cicd/runs", pageQuery(pt, nil), &resp)
		return Page[Run]{Items: resp.Runs, NextPageToken: resp.NextPageToken}, err
	})
}

// Tree — содержимое каталога репозитория на ревизии (пусто — ветка по умолчанию), без клонирования.
func (c *Client) Tree(ctx context.Context, token, repoID, revision, path string, recursive bool) ([]TreeEntry, error) {
	extra := url.Values{}
	if revision != "" {
		extra.Set("revision", revision)
	}
	if path != "" {
		extra.Set("path", path)
	}
	if recursive {
		extra.Set("recursive", "true")
	}
	return listAll(ctx, 0, func(ctx context.Context, pt string) (Page[TreeEntry], error) {
		var resp struct {
			Trees         []TreeEntry `json:"trees"`
			NextPageToken string      `json:"next_page_token"`
		}
		err := c.get(ctx, token, "/repos/id:"+url.PathEscape(repoID)+"/trees", pageQuery(pt, extra), &resp)
		return Page[TreeEntry]{Items: resp.Trees, NextPageToken: resp.NextPageToken}, err
	})
}
