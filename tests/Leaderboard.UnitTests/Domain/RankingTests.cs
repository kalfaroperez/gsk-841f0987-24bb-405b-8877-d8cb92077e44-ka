using Leaderboard.Domain;

namespace Leaderboard.UnitTests.Domain;

public class RankingTests
{
    private static Dictionary<UserId, long> Totals(params (string User, long Score)[] rows) =>
        rows.ToDictionary(r => TestData.User(r.User), r => r.Score);

    [Fact]
    public void Top_orders_by_score_descending()
    {
        IReadOnlyList<LeaderboardEntry> top = Ranking.Top(Totals(("a", 10), ("b", 30), ("c", 20)), 3);

        Assert.Equal(["b", "c", "a"], top.Select(e => e.UserId.Value));
        Assert.Equal([1, 2, 3], top.Select(e => e.Rank));
        Assert.Equal([30L, 20L, 10L], top.Select(e => e.Score));
    }

    [Fact]
    public void Ties_are_broken_by_user_id_ordinal_ascending()
    {
        IReadOnlyList<LeaderboardEntry> top = Ranking.Top(Totals(("carol", 5), ("Bob", 5), ("alice", 5)), 3);

        Assert.Equal(["Bob", "alice", "carol"], top.Select(e => e.UserId.Value));
    }

    [Fact]
    public void Top_limits_the_result_to_n_entries()
    {
        IReadOnlyList<LeaderboardEntry> top = Ranking.Top(Totals(("a", 1), ("b", 2), ("c", 3), ("d", 4)), 2);

        Assert.Equal(["d", "c"], top.Select(e => e.UserId.Value));
    }

    [Fact]
    public void Top_with_n_greater_than_users_returns_everyone()
    {
        Assert.Equal(2, Ranking.Top(Totals(("a", 1), ("b", 2)), 10).Count);
    }

    [Fact]
    public void Top_of_empty_totals_is_empty()
    {
        Assert.Empty(Ranking.Top(new Dictionary<UserId, long>(), 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Top_requires_positive_n(int n)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Ranking.Top(Totals(("a", 1)), n));
    }

    [Fact]
    public void Top_matches_a_full_sort_on_a_large_random_input()
    {
        var random = new Random(42);
        Dictionary<UserId, long> totals = Enumerable.Range(0, 5_000)
            .ToDictionary(i => TestData.User($"u{i}"), _ => (long)random.Next(0, 500));

        IEnumerable<string> expected = totals
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key.Value, StringComparer.Ordinal)
            .Take(100)
            .Select(kv => kv.Key.Value);

        Assert.Equal(expected, Ranking.Top(totals, 100).Select(e => e.UserId.Value));
    }
}
