using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

public sealed record SecurityFinding(
    string Id,
    SecurityFindingKind Kind,
    SecuritySeverity Severity,
    SecurityFindingStatus Status,
    string Title,
    string? Package,
    string? FilePath,
    double? CvssScore = null,
    int? FileLine = null,
    string? CommitSha = null);
