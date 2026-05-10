import pathlib
p = pathlib.Path(r"c:/dev/agentor/src/Agentor.Application/Coordination/SequentialAgentPlanExecutor.cs")
t = p.read_text(encoding="utf-8")
if "using Agentor.Application;" not in t:
    t = t.replace(
        "using Agentor.Application.Abstractions;",
        "using Agentor.Application;\nusing Agentor.Application.Abstractions;",
    )

deny_old = """        if (policyDecision.Outcome == PolicyDecisionOutcome.Deny)
        {
            toolCall.Deny(policyDecision.Reason, _clock.UtcNow);
            runStep.AddToolCall(toolCall);
            ps.Status = AgentPlanStepStatus.Failed;
            var fr = new FailureReason(policyDecision.ReasonCode, policyDecision.Reason, FailureCategory.Policy);"""

deny_new = """        if (policyDecision.Outcome == PolicyDecisionOutcome.Deny)
        {
            if (ExternalAgentToolKeys.IsExternalAgentTool(toolKey))
            {
                run.RecordTrace(
                    TraceEventKind.ExternalAgentInvocationDenied,
                    "External-agent tool denied by policy (not executed).",
                    _clock.UtcNow,
                    TraceData(run, plan, ps, "toolKey", toolKey, "reasonCode", policyDecision.ReasonCode));
            }
            toolCall.Deny(policyDecision.Reason, _clock.UtcNow);
            runStep.AddToolCall(toolCall);
            ps.Status = AgentPlanStepStatus.Failed;
            var fr = new FailureReason(policyDecision.ReasonCode, policyDecision.Reason, FailureCategory.Policy);"""

if deny_old not in t:
    raise SystemExit("deny pattern missing")
t = t.replace(deny_old, deny_new)

rev_old = """        if (policyDecision.Outcome == PolicyDecisionOutcome.RequiresReview)
        {
            toolCall.MarkRequiresReview(policyDecision.Reason, _clock.UtcNow);"""

rev_new = """        if (policyDecision.Outcome == PolicyDecisionOutcome.RequiresReview)
        {
            if (ExternalAgentToolKeys.IsExternalAgentTool(toolKey))
            {
                run.RecordTrace(
                    TraceEventKind.ExternalAgentInvocationRequiresReview,
                    "External-agent tool requires review (not executed).",
                    _clock.UtcNow,
                    TraceData(run, plan, ps, "toolKey", toolKey, "reasonCode", policyDecision.ReasonCode));
            }
            toolCall.MarkRequiresReview(policyDecision.Reason, _clock.UtcNow);"""

if rev_old not in t:
    raise SystemExit("review pattern missing")
t = t.replace(rev_old, rev_new)

p.write_text(t, encoding="utf-8")
print("plan executor patched")
