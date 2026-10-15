using Leaderboard.Application;
using Leaderboard.Infrastructure.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Leaderboard.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the SQLite adapter behind both ports. Call <see cref="SqliteDatabase.InitializeAsync"/> at startup.</summary>
    public static IServiceCollection AddSqliteInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddSingleton(new SqliteDatabase(connectionString));
        services.AddSingleton<SqliteScoreStore>();
        services.AddSingleton<IScoreWriteStore>(sp => sp.GetRequiredService<SqliteScoreStore>());
        services.AddSingleton<ILeaderboardReadStore>(sp => sp.GetRequiredService<SqliteScoreStore>());
        return services;
    }
}
