namespace Leaderboard.Domain;

/// <summary>Pre-aggregated sum of a user's points within one time bucket.</summary>
public sealed record ScoreBucket(UserId UserId, DateTimeOffset BucketStart, long Total);

/// <summary>
/// Reference implementation of windowed aggregation. Storage adapters implement the same rule in their
/// own query language (e.g. SQL); this is the executable specification they are tested against.
/// </summary>
public static class WindowAggregation
{
    public static IReadOnlyDictionary<UserId, long> Totals(IEnumerable<ScoreBucket> buckets, AggregationWindow window)
    {
        ArgumentNullException.ThrowIfNull(buckets);
        ArgumentNullException.ThrowIfNull(window);

        var totals = new Dictionary<UserId, long>();
        foreach (ScoreBucket bucket in buckets)
        {
            if (!window.ContainsBucket(bucket.BucketStart))
            {
                continue;
            }

            totals.TryGetValue(bucket.UserId, out long current);
            totals[bucket.UserId] = checked(current + bucket.Total);
        }

        return totals;
    }
}
