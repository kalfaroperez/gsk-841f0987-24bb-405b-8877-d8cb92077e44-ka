using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;

namespace Leaderboard.E2ETests;

/// <summary>Collects per-request latency and status codes; reports percentiles and throughput.</summary>
public sealed class LatencyRecorder(string name)
{
    private readonly ConcurrentBag<(double Ms, HttpStatusCode Status)> _samples = [];
    private readonly Stopwatch _wall = new();

    public int Count => _samples.Count;

    public void Start() => _wall.Start();

    public void Stop() => _wall.Stop();

    public async Task<HttpResponseMessage> TimeAsync(Func<Task<HttpResponseMessage>> request)
    {
        long started = Stopwatch.GetTimestamp();
        HttpResponseMessage response = await request();
        _samples.Add((Stopwatch.GetElapsedTime(started).TotalMilliseconds, response.StatusCode));
        return response;
    }

    public int CountStatus(Func<HttpStatusCode, bool> predicate) => _samples.Count(s => predicate(s.Status));

    public double Percentile(double p)
    {
        double[] sorted = _samples.Select(s => s.Ms).Order().ToArray();
        if (sorted.Length == 0)
        {
            return 0;
        }

        int index = (int)Math.Ceiling(p / 100 * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }

    public double Throughput => Count / _wall.Elapsed.TotalSeconds;

    public string Summary() => string.Create(
        CultureInfo.InvariantCulture,
        $"| {name} | {Count} | {Throughput:F0} req/s | {Percentile(50):F1} ms | {Percentile(95):F1} ms | {Percentile(99):F1} ms | {_samples.Max(s => s.Ms):F1} ms | {CountStatus(s => (int)s >= 500)} |");

    public const string Header =
        "| Escenario | Peticiones | Throughput | p50 | p95 | p99 | máx | 5xx |\n|---|---|---|---|---|---|---|---|";
}
