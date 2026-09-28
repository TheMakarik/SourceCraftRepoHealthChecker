namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class YandexIdUnavailableException(string message, Exception? innerException = null) : Exception(message, innerException);
