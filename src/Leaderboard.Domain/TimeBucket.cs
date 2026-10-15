namespace Leaderboard.Domain;

public static class TimeBucket
{
    /// <summary>Floors an instant to the start of its bucket, in UTC.</summary>
    public static DateTimeOffset StartOf(DateTimeOffset instant, TimeSpan bucketSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(bucketSize, TimeSpan.Zero);

        long ticks = instant.UtcTicks;
        return new DateTimeOffset(ticks - (ticks % bucketSize.Ticks), TimeSpan.Zero);
    }
}
