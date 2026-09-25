namespace SourceCraftRepoHealthChecker.Application.SourceCraft;

public sealed class SourceCraftOperationException(string message) : Exception(message);
