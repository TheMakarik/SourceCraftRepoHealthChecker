namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class InvalidAuthorizationCodeException(string message) : Exception(message);
