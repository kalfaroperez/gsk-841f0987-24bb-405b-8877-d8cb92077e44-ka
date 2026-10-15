using System.Net;

namespace Leaderboard.IntegrationTests.Api;

public sealed class OperationalEndpointTests : IDisposable
{
    private readonly LeaderboardApiFactory _api = new();

    public void Dispose() => _api.Dispose();

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoints_are_public_and_healthy(string url)
    {
        using HttpClient client = _api.CreateClient(apiKey: null);

        HttpResponseMessage response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Metrics_endpoint_exposes_custom_ingestion_counter()
    {
        await _api.PostScoreAsync("alice", 1);
        using HttpClient client = _api.CreateClient(apiKey: null);

        string metrics = await client.GetStringAsync("/metrics");

        Assert.Contains("leaderboard_scores_ingested", metrics, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApi_document_is_published()
    {
        using HttpClient client = _api.CreateClient(apiKey: null);

        string document = await client.GetStringAsync("/openapi/v1.json");

        Assert.Contains("/leaderboard", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Responses_carry_a_correlation_id()
    {
        using HttpClient client = _api.CreateClient(LeaderboardApiFactory.ReaderKey);

        HttpResponseMessage response = await client.GetAsync("/leaderboard");

        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out IEnumerable<string>? values));
        Assert.False(string.IsNullOrWhiteSpace(values.Single()));
    }
}
