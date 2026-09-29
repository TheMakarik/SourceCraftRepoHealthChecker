namespace SourceCraftRepoHealthChecker.Application.SourceCraft.Models;

public sealed record DocumentationReport(
    bool HasReadme,
    bool HasLicense,
    bool HasContributing,
    bool HasCodeOwners,
    bool HasLocalRunInstructions,
    bool HasBuildAndTestInstructions,
    string? LicenseId = null,
    bool HasProjectStructure = false);
