namespace SourceCraftRepoHealthChecker.Application.Ai;

public sealed class AiProviderException(string message, Exception innerException) : Exception(message, innerException);
