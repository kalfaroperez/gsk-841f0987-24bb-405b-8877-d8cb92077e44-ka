using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Leaderboard.IntegrationTests.Api;

public sealed class QueryEndpointTests : IDisposable
{
    private readonly LeaderboardApiFactory _api = new();

    public void Dispose() => _api.Dispose();

    private async Task<JsonElement> GetJson(string url, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using HttpClient client = _api.CreateClient(LeaderboardApiFactory.ReaderKey);
        HttpResponseMessage response = await client.GetAsync(url);
        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static (string, long)[] Entries(JsonElement view) =>
        view.GetProperty("entries").EnumerateArray()
            .Select(e => (e.GetProperty("user_id").GetString()!, e.GetProperty("score").GetInt64()))
            .ToArray();

    [Fact]
    public async Task Leaderboard_returns_top_n_by_windowed_sum()
    {
        await _api.PostScoreAsync("alice", 10);
        await _api.PostScoreAsync("alice", 15, _api.Clock.GetUtcNow().AddDays(-1));
        await _api.PostScoreAsync("bob", 30);
        await _api.PostScoreAsync("carol", 5);
        await _api.PostScoreAsync("carol", 999, _api.Clock.GetUtcNow().AddDays(-9));

        JsonElement view = await GetJson("/leaderboard?top=2");

        Assert.Equal([("bob", 30L), ("alice", 25L)], Entries(view));
        Assert.Equal(1, view.GetProperty("entries")[0].GetProperty("rank").GetInt32());
        Assert.True(view.TryGetProperty("window_start", out _));
        Assert.True(view.TryGetProperty("generated_at", out _));
    }

    [Fact]
    public async Task Leaderboard_uses_default_top()
    {
        for (int i = 0; i < 12; i++)
        {
            await _api.PostScoreAsync($"u{i}", i + 1);
        }

        Assert.Equal(10, Entries(await GetJson("/leaderboard")).Length);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("abc")]
    public async Task Leaderboard_rejects_invalid_top(string top)
    {
        await GetJson($"/leaderboard?top={top}", HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Leaderboard_reflects_new_scores_once_the_cached_window_moves()
    {
        await _api.PostScoreAsync("alice", 10);
        Assert.Equal([("alice", 10L)], Entries(await GetJson("/leaderboard")));

        await _api.PostScoreAsync("bob", 20);
        _api.Clock.Advance(TimeSpan.FromMinutes(1)); // new window bucket => new cache key

        Assert.Equal([("bob", 20L), ("alice", 10L)], Entries(await GetJson("/leaderboard")));
    }

    [Fact]
    public async Task User_score_returns_window_and_all_time()
    {
        await _api.PostScoreAsync("alice", 10);
        await _api.PostScoreAsync("alice", 100, _api.Clock.GetUtcNow().AddDays(-30));

        JsonElement view = await GetJson("/user/alice/score");

        Assert.Equal("alice", view.GetProperty("user_id").GetString());
        Assert.Equal(10, view.GetProperty("window_score").GetInt64());
        Assert.Equal(110, view.GetProperty("all_time_score").GetInt64());
    }

    [Fact]
    public async Task Unknown_user_returns_404_problem()
    {
        JsonElement problem = await GetJson("/user/ghost/score", HttpStatusCode.NotFound);

        Assert.Equal("user.not_found", problem.GetProperty("error_codes")[0].GetString());
    }

    [Fact]
    public async Task Invalid_user_id_returns_400()
    {
        await GetJson("/user/bad%20id/score", HttpStatusCode.BadRequest);
    }
}
