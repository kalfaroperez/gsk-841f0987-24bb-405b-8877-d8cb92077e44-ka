using System.Net;
using System.Net.Http.Json;

namespace Leaderboard.IntegrationTests.Api;

public sealed class SecurityTests
{
    [Theory]
    [InlineData("/leaderboard")]
    [InlineData("/user/alice/score")]
    public async Task Requests_without_api_key_are_rejected_with_401(string url)
    {
        using var api = new LeaderboardApiFactory();
        using HttpClient client = api.CreateClient(apiKey: null);

        HttpResponseMessage response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_api_key_is_rejected_with_401()
    {
        using var api = new LeaderboardApiFactory();
        using HttpClient client = api.CreateClient("not-a-real-key");

        HttpResponseMessage response = await client.GetAsync("/leaderboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Read_only_key_cannot_submit_scores()
    {
        using var api = new LeaderboardApiFactory();
        using HttpClient client = api.CreateClient(LeaderboardApiFactory.ReaderKey);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/score", LeaderboardApiFactory.Score("alice", 1, api.Clock.GetUtcNow()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Exceeding_the_rate_limit_returns_429_with_retry_after()
    {
        using var api = new LeaderboardApiFactory(new Dictionary<string, string?>
        {
            ["RateLimit:PermitsPerSecond"] = "1",
            ["RateLimit:Burst"] = "2",
        });
        using HttpClient client = api.CreateClient(LeaderboardApiFactory.ReaderKey);

        HttpResponseMessage[] responses = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(_ => client.GetAsync("/leaderboard")));

        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.TooManyRequests);
        HttpResponseMessage limited = responses.First(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        Assert.True(responses.Count(r => r.StatusCode == HttpStatusCode.OK) <= 3, "burst of 2 (+1 replenished at most) should be enforced");
        Assert.NotNull(limited.Headers.RetryAfter);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Startup_fails_fast_on_invalid_configuration()
    {
        using var api = new LeaderboardApiFactory(new Dictionary<string, string?>
        {
            ["Leaderboard:BucketSize"] = "00:00:07",
        });

        Assert.ThrowsAny<Exception>(() => api.CreateClient());
        await Task.CompletedTask;
    }
}
