import pathlib, re
ROOT = pathlib.Path(r"c:/dev/agentor")

# 1) TraceEventKind
tek = ROOT / "src/Agentor.Domain/Enums/TraceEventKind.cs"
t = tek.read_text(encoding="utf-8")
if "ExternalAgentInvocationCompleted" not in t:
    t = t.replace(
        "    AthanorReviewQueued\n}",
        "    AthanorReviewQueued,\n\n    ExternalAgentCapabilityDiscovered,\n    ExternalAgentInvocationStarted,\n    ExternalAgentInvocationCompleted,\n    ExternalAgentInvocationDenied,\n    ExternalAgentInvocationRequiresReview,\n    ExternalAgentInvocationFailed\n}",
    )
    tek.write_text(t, encoding="utf-8")
    print("TraceEventKind ok")

# 2) RunManifestExternalAgentTelemetry
(ROOT / "src/Agentor.Domain/RunManifestExternalAgentTelemetry.cs").write_text(
    """namespace Agentor.Domain;

public sealed record RunManifestExternalAgentTelemetry(int ExternalAgentInvocationCompletedCount)
{
    public static RunManifestExternalAgentTelemetry Empty { get; } = new(0);
}
""",
    encoding="utf-8",
)
print("external tel file ok")
