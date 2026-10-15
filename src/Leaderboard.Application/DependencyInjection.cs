using Leaderboard.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Leaderboard.Application;

public static class DependencyInjection
{
    /// <summary>Registers the use cases. Invalid configuration fails at startup, not on the first request.</summary>
    public static IServiceCollection AddLeaderboardApplication(
        this IServiceCollection services,
        LeaderboardRules rules,
        LeaderboardQueryLimits limits)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(limits);

        IReadOnlyList<string> errors = [.. rules.Validate(), .. limits.Validate()];
        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Invalid leaderboard configuration: " + string.Join(" ", errors));
        }

        services.AddSingleton(rules);
        services.AddSingleton(limits);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<SubmitScore>();
        services.AddSingleton<GetLeaderboard>();
        services.AddSingleton<GetUserScore>();
        return services;
    }
}
