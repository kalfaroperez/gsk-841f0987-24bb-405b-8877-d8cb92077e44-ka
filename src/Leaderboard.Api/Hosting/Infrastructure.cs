using Leaderboard.Infrastructure.Sqlite;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Leaderboard.Api.Hosting;

/// <summary>Creates the schema before the server accepts traffic (hosted services start before Kestrel).</summary>
internal sealed class DatabaseInitializer(SqliteDatabase database) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => database.InitializeAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class SqliteHealthCheck(SqliteDatabase database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await database.CanConnectAsync(cancellationToken).ConfigureAwait(false)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Schema not initialized.");
        }
#pragma warning disable CA1031 // A health check must report failures, not throw them.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return HealthCheckResult.Unhealthy("SQLite unavailable.", ex);
        }
    }
}
