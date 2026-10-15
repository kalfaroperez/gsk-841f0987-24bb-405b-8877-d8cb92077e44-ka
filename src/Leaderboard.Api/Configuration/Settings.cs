using Leaderboard.Application;
using Leaderboard.Domain;
using Microsoft.Extensions.Options;

namespace Leaderboard.Api.Configuration;

/// <summary>Bound from the "Leaderboard" section; mapped onto domain/application records.</summary>
public sealed class LeaderboardSettings
{
    public const string Section = "Leaderboard";

    public TimeSpan Window { get; set; } = LeaderboardRules.Default.Window;

    public TimeSpan BucketSize { get; set; } = LeaderboardRules.Default.BucketSize;

    public TimeSpan FutureSkewTolerance { get; set; } = LeaderboardRules.Default.FutureSkewTolerance;

    public long MaxPointsPerEvent { get; set; } = LeaderboardRules.Default.MaxPointsPerEvent;

    public int DefaultTop { get; set; } = LeaderboardQueryLimits.Default.DefaultTop;

    public int MaxTop { get; set; } = LeaderboardQueryLimits.Default.MaxTop;

    /// <summary>Maximum staleness of GET /leaderboard. Zero disables the cache.</summary>
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromSeconds(1);

    public LeaderboardRules ToRules() => new(Window, BucketSize, FutureSkewTolerance, MaxPointsPerEvent);

    public LeaderboardQueryLimits ToLimits() => new(DefaultTop, MaxTop);
}

public sealed class RateLimitSettings
{
    public const string Section = "RateLimit";

    /// <summary>Sustained requests per second allowed per API key (or per IP when unauthenticated).</summary>
    public int PermitsPerSecond { get; set; } = 500;

    /// <summary>Token bucket size: how many requests can be absorbed in a burst.</summary>
    public int Burst { get; set; } = 1000;
}

internal sealed class LeaderboardSettingsValidator : IValidateOptions<LeaderboardSettings>
{
    public ValidateOptionsResult Validate(string? name, LeaderboardSettings options)
    {
        List<string> errors = [.. options.ToRules().Validate(), .. options.ToLimits().Validate()];
        if (options.CacheTtl < TimeSpan.Zero)
        {
            errors.Add($"{nameof(options.CacheTtl)} cannot be negative.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}

internal sealed class RateLimitSettingsValidator : IValidateOptions<RateLimitSettings>
{
    public ValidateOptionsResult Validate(string? name, RateLimitSettings options) =>
        options.PermitsPerSecond > 0 && options.Burst >= options.PermitsPerSecond
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("RateLimit: PermitsPerSecond must be positive and Burst >= PermitsPerSecond.");
}
