using System.Diagnostics;
using System.Diagnostics.Metrics;

public static class AgentMetrics
{
    public const string MeterName = "SenthilApi.AI.Agent";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> AgentRunCounter =
        Meter.CreateCounter<long>(
            name: "senthilapi_agent_runs_total",
            unit: "{run}",
            description: "Total number of agent run attempts.");

    private static readonly Histogram<double> AgentRunDurationMs =
        Meter.CreateHistogram<double>(
            name: "senthilapi_agent_run_duration_ms",
            unit: "ms",
            description: "Duration of agent run attempts in milliseconds.");

    private static readonly Counter<long> AgentFallbackCounter =
        Meter.CreateCounter<long>(
            name: "senthilapi_agent_fallback_total",
            unit: "{fallback}",
            description: "Total number of times fallback metadata logic was used.");

    private static readonly Counter<long> AgentResultTypeCounter =
        Meter.CreateCounter<long>(
            name: "senthilapi_agent_result_content_type_total",
            unit: "{result}",
            description: "Total number of final content type classifications produced.");

    public static void RecordRun(
        string source,
        string outcome,
        double durationMs)
    {
        var tags = new TagList
        {
            { "source", source },
            { "outcome", outcome }
        };

        AgentRunCounter.Add(1, tags);
        AgentRunDurationMs.Record(durationMs, tags);
    }

    public static void RecordFallback(string source, string reason)
    {
        AgentFallbackCounter.Add(
            1,
            new TagList
            {
                { "source", source },
                { "reason", reason }
            });
    }

    public static void RecordResultType(string source, string contentType)
    {
        AgentResultTypeCounter.Add(
            1,
            new TagList
            {
                { "source", source },
                { "content_type", contentType }
            });
    }
}