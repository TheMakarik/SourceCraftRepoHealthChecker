namespace SourceCraftRepoHealthChecker.Application.Ai.Models;

public sealed record AiStreamEvent(string Type, string? Text = null);
