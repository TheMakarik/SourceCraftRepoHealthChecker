using Microsoft.Extensions.Configuration;
using Npgsql;
using SourceCraftRepoHealthChecker.Application.Scheduling;

namespace SourceCraftRepoHealthChecker.infrastructure.Scheduling;

public sealed class PostgresSchedulerLease(IConfiguration configuration) : ISchedulerLease
{
    private const long LockKey = 728110001;

    public async Task<IAsyncDisposable?> AcquireAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock($1)", connection);
        command.Parameters.AddWithValue(LockKey);
        var acquired = await command.ExecuteScalarAsync(cancellationToken) is true;

        if (!acquired)
        {
            await connection.DisposeAsync();
            return null;
        }

        return new PostgresSchedulerLeaseHandle(connection, LockKey);
    }
}
