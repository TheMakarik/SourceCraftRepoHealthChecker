using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Options;

namespace SourceCraftRepoHealthChecker.Presenter.Authentication;

public sealed class UserTicketProtector
{
    private const string ProtectorPurpose = "SourceCraftRepoHealthChecker.UserTicket";
    private readonly ITimeLimitedDataProtector _protector;
    private readonly TimeSpan _lifetime;

    public UserTicketProtector(IDataProtectionProvider dataProtectionProvider, IOptions<UserTicketOptions> options)
    {
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose).ToTimeLimitedDataProtector();
        _lifetime = TimeSpan.FromHours(Math.Max(1, options.Value.LifetimeHours));
    }

    public string Protect(Guid userId) => _protector.Protect(userId.ToString("D"), _lifetime);

    public TimeSpan Lifetime => _lifetime;

    public Guid? Unprotect(string? ticket)
    {
        if (string.IsNullOrEmpty(ticket))
            return null;

        try
        {
            var value = _protector.Unprotect(ticket);
            return Guid.TryParse(value, out var userId) ? userId : null;
        }
        catch (Exception exception) when (exception is System.Security.Cryptography.CryptographicException or FormatException)
        {
            return null;
        }
    }
}
