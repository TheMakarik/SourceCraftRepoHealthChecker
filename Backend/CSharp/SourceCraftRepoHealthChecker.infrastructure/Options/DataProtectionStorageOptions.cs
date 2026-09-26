namespace SourceCraftRepoHealthChecker.infrastructure.Options;

public sealed class DataProtectionStorageOptions
{
    public required string Endpoint { get; init; }
    public required string Bucket { get; init; }
    public required string AccessKey { get; init; }
    public required string SecretKey { get; init; }
    public string Prefix { get; init; } = "dataprotection-keys/";
    public bool UseSsl { get; init; } = true;
}
