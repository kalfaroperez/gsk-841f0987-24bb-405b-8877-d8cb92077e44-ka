using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace Leaderboard.E2ETests;

/// <summary>Thin HTTP helpers shared by the E2E scenarios.</summary>
internal static class Api
{
    /// <summary>Prefix for every user id created by this run, so runs against a shared deployment never collide.</summary>
    public static readonly string RunId = Guid.NewGuid().ToString("N")[..8];

    public static string User(string name) => $"e2e-{RunId}-{name}";

    public static Task<HttpResponseMessage> PostScoreAsync(HttpClient client, string userId, long score, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/score")
        {
            Content = JsonContent.Create(new { user_id = userId, score, timestamp = DateTimeOffset.UtcNow }),
        };
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return client.SendAsync(request);
    }

    public static async Task<long> WindowScoreAsync(HttpClient client, string userId)
    {
        JsonElement body = await client.GetFromJsonAsync<JsonElement>($"/user/{userId}/score");
        return body.GetProperty("window_score").GetInt64();
    }

    public static async Task<(int Rank, string UserId, long Score)[]> LeaderboardAsync(HttpClient client, int top)
    {
        JsonElement body = await client.GetFromJsonAsync<JsonElement>(
            string.Create(CultureInfo.InvariantCulture, $"/leaderboard?top={top}"));
        return ParseEntries(body);
    }

    public static (int Rank, string UserId, long Score)[] ParseEntries(JsonElement body) =>
        body.GetProperty("entries").EnumerateArray()
            .Select(e => (e.GetProperty("rank").GetInt32(), e.GetProperty("user_id").GetString()!, e.GetProperty("score").GetInt64()))
            .ToArray();

    /// <summary>Ranks are 1..n and scores never increase down the list.</summary>
    public static void AssertWellFormed((int Rank, string UserId, long Score)[] entries)
    {
        for (int i = 0; i < entries.Length; i++)
        {
            Assert.Equal(i + 1, entries[i].Rank);
            if (i > 0)
            {
                Assert.True(entries[i - 1].Score >= entries[i].Score, $"Leaderboard not sorted at rank {i + 1}.");
            }
        }
    }

    public static int EnvInt(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : fallback;
}
