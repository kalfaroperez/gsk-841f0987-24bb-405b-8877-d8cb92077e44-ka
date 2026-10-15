namespace Leaderboard.Domain;

public sealed record LeaderboardEntry(int Rank, UserId UserId, long Score);

/// <summary>
/// Deterministic ranking: score descending, then user id ordinal ascending. Because the order is total,
/// rank equals position and the top-N of a union equals the merge of each partition's top-N
/// (the property that makes sharded leaderboards exact).
/// </summary>
public static class Ranking
{
    public static IReadOnlyList<LeaderboardEntry> Top(IEnumerable<KeyValuePair<UserId, long>> totals, int n)
    {
        ArgumentNullException.ThrowIfNull(totals);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(n, 0);

        // Bounded min-heap of size n: O(U log n) time and O(n) memory instead of sorting all U users.
        // The heap root is the "worst" entry kept so far, i.e. the first one to evict.
        var heap = new PriorityQueue<(UserId User, long Score), (UserId User, long Score)>(n + 1, WorstFirst.Instance);
        foreach ((UserId user, long score) in totals)
        {
            heap.Enqueue((user, score), (user, score));
            if (heap.Count > n)
            {
                heap.Dequeue();
            }
        }

        var ordered = new (UserId User, long Score)[heap.Count];
        for (int i = ordered.Length - 1; i >= 0; i--)
        {
            ordered[i] = heap.Dequeue();
        }

        var entries = new LeaderboardEntry[ordered.Length];
        for (int i = 0; i < ordered.Length; i++)
        {
            entries[i] = new LeaderboardEntry(i + 1, ordered[i].User, ordered[i].Score);
        }

        return entries;
    }

    /// <summary>Orders entries from worst to best rank: lower score first, then higher user id first.</summary>
    private sealed class WorstFirst : IComparer<(UserId User, long Score)>
    {
        public static readonly WorstFirst Instance = new();

        public int Compare((UserId User, long Score) x, (UserId User, long Score) y)
        {
            int byScore = x.Score.CompareTo(y.Score);
            return byScore != 0 ? byScore : y.User.CompareTo(x.User);
        }
    }
}
