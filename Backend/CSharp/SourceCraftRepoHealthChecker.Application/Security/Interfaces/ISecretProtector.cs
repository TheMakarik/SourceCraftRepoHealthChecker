namespace SourceCraftRepoHealthChecker.Application.Security.Interfaces;

public interface ISecretProtector
{
    public string Protect(string secret);

    public string Unprotect(string protectedSecret);
}
