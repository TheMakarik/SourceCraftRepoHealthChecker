package appsec

// Типы повторяют схемы OpenAPI https://appsec.sourcecraft.tech/openapi (Quarkus).

// DefectGroupDto — группа дефектов AppSec (SAST/SCA/secret scanning и др.).
type DefectGroupDto struct {
	UUID          string  `json:"uuid"`
	PublicID      string  `json:"publicId"`
	RuleName      string  `json:"ruleName"`
	RuleID        string  `json:"ruleId"`
	CodeBlock     string  `json:"codeBlock"`
	FileName      string  `json:"fileName"`
	LatestCommit  string  `json:"latestCommit"`
	GitRepo       string  `json:"gitRepo"`
	Status        int32   `json:"status"`
	Severity      int32   `json:"severity"`
	CvssScore     float64 `json:"cvssScore"`
	Engine        string  `json:"engine"`
	EngineType    int32   `json:"engineType"`
	FindingsCount int32   `json:"findingsCount"`
	StartLine     int32   `json:"startLine"`
	EndLine       int32   `json:"endLine"`
}

// PaginatedDefectGroupsDto — ответ GET /v1/defect-groups.
type PaginatedDefectGroupsDto struct {
	Data          []DefectGroupDto `json:"data"`
	NextPageToken string           `json:"nextPageToken"`
	TotalSize     int              `json:"totalSize"`
}
