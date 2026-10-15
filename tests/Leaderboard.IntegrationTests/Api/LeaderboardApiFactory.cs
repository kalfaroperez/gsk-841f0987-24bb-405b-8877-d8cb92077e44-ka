using System.Globalization;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Leaderboard.IntegrationTests.Api;

/// <summary>
/// Hosts the real API in memory against a temporary SQLite file, with a controllable clock and test API keys.
/// </summary>
public sealed class LeaderboardApiFactory(IReadOnlyDictionary<string, string?>? overrides = null)
    : WebApplicationFactory<Program>
{
    public const string WriterKey = "test-writer-key";
    public const string ReaderKey = "test-reader-key";

    public static readonly DateTimeOffset StartTime =
        DateTimeOffset.Parse("2026-10-15T12:00:30Z", CultureInfo.InvariantCulture);

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"leaderboard-api-{Guid.NewGuid():N}.db");

    public FakeTimeProvider Clock { get; } = new(StartTime);

    public HttpClient CreateClient(string? apiKey)
    {
        HttpClient client = CreateClient();
        if (apiKey is not null)
        {
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        }

        return client;
    }

    public static object Score(string userId, long score, DateTimeOffset timestamp) =>
        new { user_id = userId, score, timestamp };

    public async Task<HttpResponseMessage> PostScoreAsync(string userId, long score, DateTimeOffset? at = null)
    {
        using HttpClient client = CreateClient(WriterKey);
        return await client.PostAsJsonAsync("/score", Score(userId, score, at ?? Clock.GetUtcNow()));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(config =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Leaderboard"] = $"Data Source={_dbPath}",
                ["Auth:ApiKeys:0:Name"] = "test-writer",
                ["Auth:ApiKeys:0:Key"] = WriterKey,
                ["Auth:ApiKeys:0:Roles:0"] = "scores:write",
                ["Auth:ApiKeys:0:Roles:1"] = "scores:read",
                ["Auth:ApiKeys:1:Name"] = "test-reader",
                ["Auth:ApiKeys:1:Key"] = ReaderKey,
                ["Auth:ApiKeys:1:Roles:0"] = "scores:read",
                ["RateLimit:PermitsPerSecond"] = "100000",
                ["RateLimit:Burst"] = "100000",
            };

            foreach ((string key, string? value) in overrides ?? new Dictionary<string, string?>())
            {
                settings[key] = value;
            }

            config.AddInMemoryCollection(settings);
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        foreach (string file in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
        {
            File.Delete(file);
        }
    }
}
