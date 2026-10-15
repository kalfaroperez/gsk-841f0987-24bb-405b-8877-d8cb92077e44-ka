using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Leaderboard.IntegrationTests.Api;

public sealed class ScoreEndpointTests : IDisposable
{
    private readonly LeaderboardApiFactory _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Valid_score_returns_201_with_event_id_and_location()
    {
        HttpResponseMessage response = await _api.PostScoreAsync("alice", 50);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("created", body.GetProperty("status").GetString());
        Assert.StartsWith("h:", body.GetProperty("event_id").GetString(), StringComparison.Ordinal);
        Assert.Equal("/user/alice/score", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Retry_with_same_idempotency_key_returns_200_duplicate()
    {
        using HttpClient client = _api.CreateClient(LeaderboardApiFactory.WriterKey);
        object payload = LeaderboardApiFactory.Score("alice", 50, _api.Clock.GetUtcNow());

        HttpResponseMessage first = await SendWithKey(client, payload, "req-42");
        HttpResponseMessage retry = await SendWithKey(client, payload, "req-42");
        JsonElement body = await retry.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal("duplicate", body.GetProperty("status").GetString());
        Assert.Equal("k:req-42", body.GetProperty("event_id").GetString());
    }

    [Fact]
    public async Task Invalid_fields_return_validation_problem_with_all_errors()
    {
        using HttpClient client = _api.CreateClient(LeaderboardApiFactory.WriterKey);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/score", LeaderboardApiFactory.Score("bad id", -5, _api.Clock.GetUtcNow().AddDays(1)));
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        JsonElement errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("user_id", out _));
        Assert.True(errors.TryGetProperty("score", out _));
        Assert.True(errors.TryGetProperty("timestamp", out _));
        Assert.Contains("score.out_of_range", problem.GetProperty("error_codes").EnumerateArray().Select(c => c.GetString()));
    }

    [Fact]
    public async Task Missing_required_fields_return_400()
    {
        using HttpClient client = _api.CreateClient(LeaderboardApiFactory.WriterKey);

        HttpResponseMessage response = await client.PostAsJsonAsync("/score", new { user_id = "alice" });
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(problem.GetProperty("errors").TryGetProperty("score", out _));
        Assert.True(problem.GetProperty("errors").TryGetProperty("timestamp", out _));
    }

    [Fact]
    public async Task Malformed_json_returns_problem_details_400()
    {
        using HttpClient client = _api.CreateClient(LeaderboardApiFactory.WriterKey);
        using var content = new StringContent("{ not json", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PostAsync("/score", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Invalid_idempotency_key_returns_400()
    {
        using HttpClient client = _api.CreateClient(LeaderboardApiFactory.WriterKey);

        HttpResponseMessage response = await SendWithKey(
            client, LeaderboardApiFactory.Score("alice", 1, _api.Clock.GetUtcNow()), new string('x', 200));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static Task<HttpResponseMessage> SendWithKey(HttpClient client, object payload, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/score") { Content = JsonContent.Create(payload) };
        request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }
}
