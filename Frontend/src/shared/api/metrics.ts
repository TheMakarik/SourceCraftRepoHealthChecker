export const metricLabels: Record<string, string> = {
  SecurityCriticalFindings: "Critical",
  SecurityHighFindings: "High",
  SecurityMediumFindings: "Medium",
  SecurityLowFindings: "Low",
  SecurityFixedFindings: "Исправлено находок",
  CodeHealthTodo: "TODO",
  CodeHealthFixme: "FIXME",
  CodeHealthStaleComments: "Старейший маркер (дн.)",
  ActivityLastActivity: "Последняя активность (дн. назад)",
  ActivityCommitFrequency: "Коммитов за период",
  ActivityContributors: "Контрибьюторов",
  ActivityReleases: "Релизов",
  ActivityMergeRequests: "Merge requests",
  DocumentationReadme: "README",
  DocumentationLicense: "Лицензия",
  DocumentationContributing: "CONTRIBUTING",
  DocumentationCodeOwners: "CODEOWNERS",
  DocumentationLocalRun: "Инструкция локального запуска",
  DocumentationBuildAndTest: "Инструкции сборки/тестов",
  CiCdPresence: "Наличие CI/CD",
  CiCdSuccessRatio: "Доля успешных (%)",
  CiCdPipelineDuration: "Средняя длительность (мин)",
  CiCdStability: "Стабильность",
  IssuesOpen: "Открытых",
  IssuesClosed: "Закрытых",
  IssuesStale: "Зависших (>30 дн.)",
  IssuesFirstResponse: "Ср. время первого ответа (дн.)",
  IssuesCloseTime: "Ср. время закрытия (дн.)"
};

export function metricLabel(code: string): string {
  return metricLabels[code] ?? code;
}
