import pathlib
ROOT = pathlib.Path(r"c:/dev/agentor")
(ROOT / "src/Agentor.Application/Manifest/ExternalAgentTelemetryAggregator.cs").write_text(
    """using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Manifest;

public static class ExternalAgentTelemetryAggregator
{
    public static RunManifestExternalAgentTelemetry Aggregate(AgentRun run)
    {
        var n = run.Trace.Count(e => e.Kind == TraceEventKind.ExternalAgentInvocationCompleted);
        return new RunManifestExternalAgentTelemetry(n);
    }
}
""",
    encoding="utf-8",
)

gh = ROOT / "src/Agentor.Application/Queries/GetRunManifestQueryHandler.cs"
t = gh.read_text(encoding="utf-8")
t = t.replace(
    "using Agentor.Application.Manifest;",
    "using Agentor.Application.Manifest;",
)
if "ExternalAgentTelemetryAggregator" not in t:
    t = t.replace(
        "using Agentor.Application.Manifest;",
        "using Agentor.Application.Manifest;\n",
    )
    t = t.replace(
        "RunManifest.FromRun(run, ModelCallTelemetryAggregator.Aggregate(run))",
        "RunManifest.FromRun(run, ModelCallTelemetryAggregator.Aggregate(run), ExternalAgentTelemetryAggregator.Aggregate(run))",
    )
gh.write_text(t, encoding="utf-8")
print("handler patched")
