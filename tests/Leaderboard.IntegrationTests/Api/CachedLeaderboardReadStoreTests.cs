using System.Globalization;
using Leaderboard.Application;
using Leaderboard.Domain;
using Leaderboard.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Leaderboard.IntegrationTests.Api;

/// <summary>Read hot-key mitigation: concurrent identical top-N requests must collapse into one store query.</summary>
public sealed class CachedLeaderboardReadStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-15T12:00:30Z", CultureInfo.InvariantCulture);
    private static readonly AggregationWindow Window = AggregationWindow.EndingAt(Now, LeaderboardRules.Default);

    private readonly ServiceProvider _services = new ServiceCollection().AddHybridCache().Services.BuildServiceProvider();
    private readonly SlowCountingStore _inner = new();
    private readonly CachedLeaderboardReadStore _sut;

    public CachedLeaderboardReadStoreTests() =>
        _sut = new CachedLeaderboardReadStore(_inner, _services.GetRequiredService<HybridCache>(), TimeSpan.FromSeconds(30));

    public void Dispose() => _services.Dispose();

    [Fact]
    public async Task Concurrent_identical_requests_hit_the_store_once()
    {
        LeaderboardSnapshot[] results = await Task.WhenAll(
            Enumerable.Range(0, 50).Select(_ => _sut.GetTopAsync(Window, 10, CancellationToken.None)));

        Assert.Equal(1, _inner.TopCalls);
        Assert.All(results, r => Assert.Equal("alice", r.Entries[0].UserId.Value));
    }

    [Fact]
    public async Task Different_top_or_window_are_cached_separately()
    {
        await _sut.GetTopAsync(Window, 10, CancellationToken.None);
        await _sut.GetTopAsync(Window, 5, CancellationToken.None);
        await _sut.GetTopAsync(AggregationWindow.EndingAt(Now.AddMinutes(1), LeaderboardRules.Default), 10, CancellationToken.None);
        await _sut.GetTopAsync(Window, 10, CancellationToken.None);

        Assert.Equal(3, _inner.TopCalls);
    }

    [Fact]
    public async Task User_scores_are_not_cached()
    {
        UserId alice = UserId.Create("alice").Value;
        await _sut.GetUserScoreAsync(alice, Window, CancellationToken.None);
        await _sut.GetUserScoreAsync(alice, Window, CancellationToken.None);

        Assert.Equal(2, _inner.UserCalls);
    }

    private sealed class SlowCountingStore : ILeaderboardReadStore
    {
        private int _topCalls;
        private int _userCalls;

        public int TopCalls => Volatile.Read(ref _topCalls);

        public int UserCalls => Volatile.Read(ref _userCalls);

        public async Task<LeaderboardSnapshot> GetTopAsync(AggregationWindow window, int top, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _topCalls);
            await Task.Delay(100, cancellationToken);
            return new LeaderboardSnapshot([new LeaderboardEntry(1, UserId.Create("alice").Value, 42)], Now);
        }

        public Task<UserScoreSnapshot?> GetUserScoreAsync(UserId userId, AggregationWindow window, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _userCalls);
            return Task.FromResult<UserScoreSnapshot?>(new UserScoreSnapshot(1, 1));
        }
    }
}
