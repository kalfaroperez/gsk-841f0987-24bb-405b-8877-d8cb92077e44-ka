namespace Leaderboard.Domain;

/// <summary>Configurable business rules. Infrastructure maps configuration onto this record.</summary>
public sealed record LeaderboardRules(
    TimeSpan Window,
    TimeSpan BucketSize,
    TimeSpan FutureSkewTolerance,
    long MaxPointsPerEvent)
{
    public static LeaderboardRules Default { get; } = new(
        Window: TimeSpan.FromDays(7),
        BucketSize: TimeSpan.FromMinutes(1),
        FutureSkewTolerance: TimeSpan.FromMinutes(5),
        MaxPointsPerEvent: 1_000_000);

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (BucketSize <= TimeSpan.Zero || BucketSize > TimeSpan.FromDays(1)
            || TimeSpan.FromDays(1).Ticks % BucketSize.Ticks != 0)
        {
            // Dividing a day keeps buckets aligned to UTC midnight, so every node computes identical boundaries.
            errors.Add($"{nameof(BucketSize)} must be positive, at most one day, and divide a day evenly.");
        }

        if (Window <= TimeSpan.Zero || (BucketSize > TimeSpan.Zero && Window.Ticks % BucketSize.Ticks != 0))
        {
            errors.Add($"{nameof(Window)} must be a positive multiple of {nameof(BucketSize)}.");
        }

        if (FutureSkewTolerance < TimeSpan.Zero)
        {
            errors.Add($"{nameof(FutureSkewTolerance)} cannot be negative.");
        }

        if (MaxPointsPerEvent <= 0)
        {
            errors.Add($"{nameof(MaxPointsPerEvent)} must be positive.");
        }

        return errors;
    }
}
