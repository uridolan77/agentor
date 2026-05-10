using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Agentor.Api.Observability;

/// <summary>
/// In-process observability hooks (Activity + metrics). Exporters/listeners are host concerns; this PR wires correlation and counters only.
/// </summary>
public static class AgentorDiagnostics
{
    public const string ActivitySourceName = "Agentor.Api";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "0.1.0");

    public static readonly Meter Meter = new("Agentor.Api", "0.1.0");

    public static readonly Counter<long> HttpServerRequestCount =
        Meter.CreateCounter<long>("agentor.http.server.request.count", description: "HTTP requests observed by Agentor request middleware.");
}
