using System.Globalization;
using Leaderboard.Application;
using Leaderboard.Domain;
using Microsoft.Extensions.Caching.Hybrid;

namespace Leaderboard.Infrastructure.Caching;

/// <summary>
/// Read hot-key mitigation for the global top-N: every client asks for the same few keys, so results are cached
/// for a short TTL and concurrent misses for the same key are coalesced into a single store query
/// (HybridCache stampede protection). The snapshot keeps its original read time, so staleness stays visible.
/// User scores are not cached: they are per-key, cheap and expected to reflect the caller's own writes.
/// </summary>
public sealed class CachedLeaderboardReadStore(ILeaderboardReadStore inner, HybridCache cache, TimeSpan ttl)
    : ILeaderboardReadStore
{
    private readonly HybridCacheEntryOptions _entryOptions = new() { Expiration = ttl, LocalCacheExpiration = ttl };

    public async Task<LeaderboardSnapshot> GetTopAsync(AggregationWindow window, int top, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (ttl <= TimeSpan.Zero)
        {
            return await inner.GetTopAsync(window, top, cancellationToken).ConfigureAwait(false);
        }

        // The window is part of the key: when it slides to the next bucket, the key changes and stale data is never reused.
        string key = string.Create(
            CultureInfo.InvariantCulture,
            $"leaderboard:top:{window.Start.UtcTicks}:{window.End.UtcTicks}:{top}");

        return await cache.GetOrCreateAsync(
            key,
            (inner, window, top),
            static (state, token) => new ValueTask<LeaderboardSnapshot>(state.inner.GetTopAsync(state.window, state.top, token)),
            _entryOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public Task<UserScoreSnapshot?> GetUserScoreAsync(UserId userId, AggregationWindow window, CancellationToken cancellationToken) =>
        inner.GetUserScoreAsync(userId, window, cancellationToken);
}
