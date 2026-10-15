using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Leaderboard.E2ETests;

[Collection(E2EGroup.Name)]
public sealed class UserJourneyTests(ServiceUnderTest service)
{
    [Fact]
    public async Task Ingest_retry_and_query_journey()
    {
        using HttpClient writer = service.CreateClient(service.WriterKey);
        using HttpClient reader = service.CreateClient(service.ReaderKey);
        string alice = Api.User("alice");
        string bob = Api.User("bob");

        // Large scores keep this run's users inside the top 100 even on a shared deployment.
        HttpResponseMessage created = await Api.PostScoreAsync(writer, alice, 1_000_000, idempotencyKey: $"{alice}-1");
        HttpResponseMessage retried = await Api.PostScoreAsync(writer, alice, 1_000_000, idempotencyKey: $"{alice}-1");
        await Api.PostScoreAsync(writer, alice, 1_000_000, idempotencyKey: $"{alice}-2");
        await Api.PostScoreAsync(writer, bob, 1_000_000, idempotencyKey: $"{bob}-1");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Assert.Equal("duplicate", (await retried.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        Assert.Equal(2_000_000, await Api.WindowScoreAsync(reader, alice));
        Assert.Equal(1_000_000, await Api.WindowScoreAsync(reader, bob));

        await Task.Delay(service.CacheTtl + TimeSpan.FromMilliseconds(200));
        (int Rank, string UserId, long Score)[] top = await Api.LeaderboardAsync(reader, 100);

        Api.AssertWellFormed(top);
        int aliceRank = Array.FindIndex(top, e => e.UserId == alice);
        int bobRank = Array.FindIndex(top, e => e.UserId == bob);
        Assert.True(aliceRank >= 0 && bobRank > aliceRank, "alice (2M) must rank above bob (1M)");
    }

    [Fact]
    public async Task Security_boundaries_hold_over_real_http()
    {
        using HttpClient anonymous = service.CreateClient(apiKey: null);
        using HttpClient reader = service.CreateClient(service.ReaderKey);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(new Uri("/leaderboard", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Api.PostScoreAsync(reader, Api.User("x"), 1)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync(new Uri("/health/ready", UriKind.Relative))).StatusCode);
    }
}
