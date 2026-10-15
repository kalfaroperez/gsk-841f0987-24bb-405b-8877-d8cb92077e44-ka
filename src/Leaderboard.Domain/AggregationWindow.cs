namespace Leaderboard.Domain;

/// <summary>
/// Half-open interval [Start, End) aligned to bucket boundaries. It always contains the current bucket
/// and spans exactly <see cref="LeaderboardRules.Window"/>, so it covers a fixed number of buckets:
/// the read cost is bounded by Window / BucketSize regardless of event volume.
/// Precision trade-off: the oldest edge moves in BucketSize steps, not continuously.
/// </summary>
public sealed record AggregationWindow
{
    private AggregationWindow(DateTimeOffset start, DateTimeOffset end, TimeSpan bucketSize)
    {
        Start = start;
        End = end;
        BucketSize = bucketSize;
    }

    public DateTimeOffset Start { get; }

    public DateTimeOffset End { get; }

    public TimeSpan BucketSize { get; }

    public long BucketCount => (End - Start).Ticks / BucketSize.Ticks;

    public static AggregationWindow EndingAt(DateTimeOffset now, LeaderboardRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        DateTimeOffset end = TimeBucket.StartOf(now, rules.BucketSize) + rules.BucketSize;
        return new AggregationWindow(end - rules.Window, end, rules.BucketSize);
    }

    public bool Contains(DateTimeOffset instant) => instant >= Start && instant < End;

    public bool ContainsBucket(DateTimeOffset bucketStart) => Contains(bucketStart);
}
