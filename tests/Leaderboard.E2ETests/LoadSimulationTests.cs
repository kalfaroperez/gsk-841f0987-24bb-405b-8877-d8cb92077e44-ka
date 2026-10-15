using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Leaderboard.E2ETests;

/// <summary>
/// Simulates realistic ingestion against the running service and measures client-side latency.
/// Volumes are configurable: E2E_WRITES (default 5000), E2E_USERS (500), E2E_CONCURRENCY (64).
/// Latency thresholds are deliberately not asserted (they depend on the machine); correctness is.
/// </summary>
[Collection(E2EGroup.Name)]
public sealed class LoadSimulationTests(ServiceUnderTest service, ITestOutputHelper output)
{
    private static readonly int Writes = Api.EnvInt("E2E_WRITES", 5_000);
    private static readonly int Users = Api.EnvInt("E2E_USERS", 500);
    private static readonly int Concurrency = Api.EnvInt("E2E_CONCURRENCY", 64);

    [Fact]
    public async Task Concurrent_ingestion_with_hot_user_and_retries_loses_no_updates()
    {
        using HttpClient writer = service.CreateClient(service.WriterKey);
        using HttpClient reader = service.CreateClient(service.ReaderKey);
        var expected = new ConcurrentDictionary<string, long>();
        var writes = new LatencyRecorder("POST /score (30 % a un hot user, 5 % reintentos)");
        string hotUser = Api.User("hot");

        writes.Start();
        await Parallel.ForEachAsync(
            Enumerable.Range(0, Writes),
            new ParallelOptions { MaxDegreeOfParallelism = Concurrency },
            async (i, ct) =>
            {
                // Deterministic per-event randomness keeps the run reproducible.
                var rnd = new Random(i);
                string user = rnd.NextDouble() < 0.30 ? hotUser : Api.User($"u{rnd.Next(Users)}");
                long points = rnd.Next(1, 101);
                string key = $"{Api.RunId}-{i}";

                using HttpResponseMessage first = await writes.TimeAsync(() => Api.PostScoreAsync(writer, user, points, key));
                Assert.Equal(HttpStatusCode.Created, first.StatusCode);
                expected.AddOrUpdate(user, points, (_, total) => total + points);

                if (rnd.NextDouble() < 0.05)
                {
                    // Simulated client retry after a timeout: must be acknowledged but not counted again.
                    using HttpResponseMessage retry = await writes.TimeAsync(() => Api.PostScoreAsync(writer, user, points, key));
                    Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
                }
            });
        writes.Stop();

        var reads = new LatencyRecorder("GET /user/{id}/score (verificación)");
        reads.Start();
        await Parallel.ForEachAsync(expected, new ParallelOptions { MaxDegreeOfParallelism = Concurrency }, async (kv, ct) =>
        {
            using HttpResponseMessage response = await reads.TimeAsync(() => reader.GetAsync(new Uri($"/user/{kv.Key}/score", UriKind.Relative), ct));
            JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            Assert.Equal(kv.Value, body.GetProperty("window_score").GetInt64());
        });
        reads.Stop();

        Report(writes, reads);
        Assert.Equal(0, writes.CountStatus(s => (int)s >= 500));
        Assert.Equal(expected[hotUser], await Api.WindowScoreAsync(reader, hotUser));
    }

    [Fact]
    public async Task Leaderboard_reads_stay_fast_and_consistent_during_writes()
    {
        using HttpClient writer = service.CreateClient(service.WriterKey);
        using HttpClient reader = service.CreateClient(service.ReaderKey);
        var reads = new LatencyRecorder("GET /leaderboard?top=10 (durante escrituras)");
        var writes = new LatencyRecorder("POST /score (concurrente con lecturas)");
        using var writersDone = new CancellationTokenSource();

        reads.Start();
        Task readers = Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            while (!writersDone.IsCancellationRequested)
            {
                using HttpResponseMessage response = await reads.TimeAsync(() => reader.GetAsync(new Uri("/leaderboard?top=10", UriKind.Relative)));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Api.AssertWellFormed(Api.ParseEntries(await response.Content.ReadFromJsonAsync<JsonElement>()));
            }
        })));

        writes.Start();
        await Parallel.ForEachAsync(
            Enumerable.Range(0, Writes / 2),
            new ParallelOptions { MaxDegreeOfParallelism = Concurrency / 2 },
            async (i, ct) =>
            {
                using HttpResponseMessage r = await writes.TimeAsync(() =>
                    Api.PostScoreAsync(writer, Api.User($"m{i % Users}"), 10, $"{Api.RunId}-mixed-{i}"));
                Assert.Equal(HttpStatusCode.Created, r.StatusCode);
            });
        writes.Stop();
        await writersDone.CancelAsync();
        await readers;
        reads.Stop();

        Report(writes, reads);
        Assert.Equal(0, reads.CountStatus(s => (int)s >= 500));
        Assert.Equal(0, writes.CountStatus(s => (int)s >= 500));
    }

    [Fact]
    public async Task Retry_storm_of_one_request_is_counted_exactly_once()
    {
        using HttpClient writer = service.CreateClient(service.WriterKey);
        using HttpClient reader = service.CreateClient(service.ReaderKey);
        string user = Api.User("storm");
        string key = $"{Api.RunId}-storm";

        HttpStatusCode[] statuses = await Task.WhenAll(Enumerable.Range(0, 200).Select(async _ =>
        {
            using HttpResponseMessage r = await Api.PostScoreAsync(writer, user, 42, key);
            return r.StatusCode;
        }));

        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.Created));
        Assert.Equal(199, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.Equal(42, await Api.WindowScoreAsync(reader, user));
    }

    private void Report(params LatencyRecorder[] recorders)
    {
        string table = $"""
            Objetivo: {service.Mode} · escrituras={Writes} · usuarios={Users} · concurrencia={Concurrency}

            {LatencyRecorder.Header}
            {string.Join(Environment.NewLine, recorders.Select(r => r.Summary()))}

            """;
        output.WriteLine(table);

        string path = Environment.GetEnvironmentVariable("E2E_REPORT_PATH")
            ?? Path.Combine(AppContext.BaseDirectory, "e2e-report.md");
        File.AppendAllText(path, table + Environment.NewLine);
        output.WriteLine($"Report appended to {path}");
    }
}
