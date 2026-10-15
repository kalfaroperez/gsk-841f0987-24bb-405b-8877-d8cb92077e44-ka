namespace Leaderboard.Application;

/// <summary>Bounds on read queries; a capped <c>top</c> keeps response size and query cost predictable.</summary>
public sealed record LeaderboardQueryLimits(int DefaultTop, int MaxTop)
{
    public static LeaderboardQueryLimits Default { get; } = new(DefaultTop: 10, MaxTop: 100);

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (MaxTop <= 0)
        {
            errors.Add($"{nameof(MaxTop)} must be positive.");
        }

        if (DefaultTop <= 0 || DefaultTop > MaxTop)
        {
            errors.Add($"{nameof(DefaultTop)} must be between 1 and {nameof(MaxTop)}.");
        }

        return errors;
    }
}
