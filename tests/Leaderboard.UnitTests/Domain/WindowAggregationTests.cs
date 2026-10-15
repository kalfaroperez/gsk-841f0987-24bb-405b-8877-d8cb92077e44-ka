using Leaderboard.Domain;

namespace Leaderboard.UnitTests.Domain;

public class WindowAggregationTests
{
    private static readonly DateTimeOffset Now = TestData.At("2026-10-15T12:00:30Z");
    private static readonly AggregationWindow Window = AggregationWindow.EndingAt(Now, TestData.Rules);
    private static readonly UserId Alice = TestData.User("alice");
    private static readonly UserId Bob = TestData.User("bob");

    [Fact]
    public void Totals_sum_buckets_per_user_inside_the_window()
    {
        ScoreBucket[] buckets =
        [
            new(Alice, TestData.At("2026-10-15T12:00:00Z"), 10),
            new(Alice, TestData.At("2026-10-14T08:00:00Z"), 15),
            new(Bob, TestData.At("2026-10-10T00:00:00Z"), 7),
        ];

        IReadOnlyDictionary<UserId, long> totals = WindowAggregation.Totals(buckets, Window);

        Assert.Equal(25, totals[Alice]);
        Assert.Equal(7, totals[Bob]);
    }

    [Fact]
    public void Totals_ignore_buckets_outside_the_window()
    {
        ScoreBucket[] buckets =
        [
            new(Alice, Window.Start, 1),
            new(Alice, Window.Start.AddMinutes(-1), 100),
            new(Alice, Window.End, 1000),
            new(Bob, Window.Start.AddDays(-1), 5),
        ];

        IReadOnlyDictionary<UserId, long> totals = WindowAggregation.Totals(buckets, Window);

        Assert.Equal(1, totals[Alice]);
        Assert.False(totals.ContainsKey(Bob));
    }

    [Fact]
    public void Totals_of_no_buckets_is_empty()
    {
        Assert.Empty(WindowAggregation.Totals([], Window));
    }

    [Fact]
    public void Totals_fail_loudly_on_overflow()
    {
        ScoreBucket[] buckets =
        [
            new(Alice, Window.Start, long.MaxValue),
            new(Alice, Window.Start.AddMinutes(1), 1),
        ];

        Assert.Throws<OverflowException>(() => WindowAggregation.Totals(buckets, Window));
    }
}
