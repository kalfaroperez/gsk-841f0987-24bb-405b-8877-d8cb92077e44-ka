using System.Diagnostics.Metrics;
using Leaderboard.Application;

namespace Leaderboard.Api.Observability;

/// <summary>Business metrics. HTTP latency/throughput come from the ASP.NET Core OpenTelemetry instrumentation.</summary>
public sealed class LeaderboardMetrics
{
    public const string MeterName = "Leaderboard.Api";

    private readonly Counter<long> _ingested;

    public LeaderboardMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter meter = meterFactory.Create(MeterName);
        _ingested = meter.CreateCounter<long>(
            "leaderboard.scores.ingested",
            description: "Score submissions by outcome (created, duplicate, rejected).");
    }

    public void ScoreAccepted(AppendOutcome outcome) =>
        _ingested.Add(1, new KeyValuePair<string, object?>("outcome", outcome == AppendOutcome.Created ? "created" : "duplicate"));

    public void ScoreRejected() =>
        _ingested.Add(1, new KeyValuePair<string, object?>("outcome", "rejected"));
}
