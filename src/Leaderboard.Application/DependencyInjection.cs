using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Leaderboard.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the use cases. The host must register <see cref="Domain.LeaderboardRules"/> and
    /// <see cref="LeaderboardQueryLimits"/> (validated at startup) and the store ports.
    /// </summary>
    public static IServiceCollection AddLeaderboardApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<SubmitScore>();
        services.AddSingleton<GetLeaderboard>();
        services.AddSingleton<GetUserScore>();
        return services;
    }
}
