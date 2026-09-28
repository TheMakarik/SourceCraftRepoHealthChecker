namespace SourceCraftRepoHealthChecker.Application.Security.Interfaces;

public interface IAiTokenProtector
{
    public string Protect(string token);

    public string Unprotect(string protectedToken);
}
