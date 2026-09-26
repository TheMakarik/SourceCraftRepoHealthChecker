using Npgsql;

namespace SourceCraftRepoHealthChecker.infrastructure.Scheduling;

public sealed class PostgresSchedulerLeaseHandle(NpgsqlConnection connection, long lockKey) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        try
        {
            await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock($1)", connection);
            command.Parameters.AddWithValue(lockKey);
            await command.ExecuteScalarAsync();
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }
}
