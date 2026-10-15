using Leaderboard.Application;
using Leaderboard.Infrastructure.Caching;
using Leaderboard.Infrastructure.Sqlite;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Leaderboard.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the SQLite adapter (write port) and the cached read port. Settings are resolved lazily from the
    /// container so hosts and tests can override configuration. Call <see cref="SqliteDatabase.InitializeAsync"/> at startup.
    /// </summary>
    public static IServiceCollection AddLeaderboardInfrastructure(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionString,
        Func<IServiceProvider, TimeSpan> leaderboardCacheTtl)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        ArgumentNullException.ThrowIfNull(leaderboardCacheTtl);

        services.AddHybridCache();
        services.AddSingleton(sp => new SqliteDatabase(connectionString(sp)));
        services.AddSingleton<SqliteScoreStore>();
        services.AddSingleton<IScoreWriteStore>(sp => sp.GetRequiredService<SqliteScoreStore>());
        services.AddSingleton<ILeaderboardReadStore>(sp => new CachedLeaderboardReadStore(
            sp.GetRequiredService<SqliteScoreStore>(),
            sp.GetRequiredService<HybridCache>(),
            leaderboardCacheTtl(sp)));
        return services;
    }
}
