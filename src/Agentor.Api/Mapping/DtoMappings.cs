using Agentor.Contracts;
using Agentor.Domain;
using Agentor.Domain.Governance;
using Agentor.Domain.Policy;

namespace Agentor.Api.Mapping;

public static class DtoMappings
{
    public static AgentRunSummaryDto ToDto(this AgentRunSummary summary)
    {
        return new AgentRunSummaryDto(
            summary.Id,
            summary.ProfileId,
            summary.TenantId,
            summary.WorkspaceId,
            summary.ProjectId,
            summary.KnowledgeScopeId,
            summary.AgentName,
            summary.TraceId,
            summary.Status,
            summary.StartedAt,
            summary.CompletedAt,
            summary.ErrorMessage);
    }

    public static AgentRunListResponseDto ToDto(this AgentRunListPage page)
    {
        return new AgentRunListResponseDto(
            page.Items.Select(ToDto).ToList(),
            page.TotalCount,
            page.Skip,
            page.Take);
    }

    public static AgentRunDto ToDto(this AgentRun run)
    {
        return new AgentRunDto(
            run.Id,
            run.ProfileId,
            run.TenantId,
            run.WorkspaceId,
            run.ProjectId,
            run.KnowledgeScopeId,
            run.ResolveAthanorProjectId(),
            run.AgentName,
            run.Objective,
            run.TraceId,
            run.Status,
            run.StartedAt,
            run.CompletedAt,
            run.ErrorMessage,
            run.Steps.Select(ToDto).ToList(),
            run.Trace.Select(ToDto).ToList(),
            run.HumanReviewDecisions.Select(d => d.ToDto()).ToList());
    }

    public static AgentStepDto ToDto(this AgentStep step)
    {
        return new AgentStepDto(
            step.Id,
            step.Index,
            step.Name,
            step.Status,
            step.StartedAt,
            step.CompletedAt,
            step.PolicyDecisions.Select(ToDto).ToList(),
            step.ToolCalls.Select(ToDto).ToList());
    }

    public static PolicyDecisionDto ToDto(this PolicyDecision decision)
    {
        return new PolicyDecisionDto(
            decision.Id,
            decision.Outcome,
            decision.ReasonCode,
            decision.Reason,
            decision.DecidedAt);
    }

    public static HumanReviewDecisionDto ToDto(this HumanReviewDecision decision)
    {
        return new HumanReviewDecisionDto(
            decision.Id,
            decision.Kind,
            decision.ActorId,
            decision.DecidedAt,
            decision.Note,
            decision.Resolution);
    }

    public static ToolCallDto ToDto(this ToolCall toolCall)
    {
        return new ToolCallDto(
            toolCall.Id,
            toolCall.ToolKey,
            toolCall.Status,
            toolCall.Input,
            toolCall.Output,
            toolCall.StartedAt,
            toolCall.CompletedAt,
            toolCall.ErrorMessage);
    }

    public static TraceEventDto ToDto(this ExecutionTraceEvent traceEvent)
    {
        return new TraceEventDto(
            traceEvent.Id,
            traceEvent.Kind,
            traceEvent.Message,
            traceEvent.OccurredAt,
            traceEvent.Data);
    }

    public static RunManifestDto ToDto(this RunManifest manifest)
    {
        return new RunManifestDto(
            manifest.RunId,
            manifest.ProfileId,
            manifest.TraceId,
            manifest.Status,
            manifest.StartedAt,
            manifest.CompletedAt,
            manifest.StepCount,
            manifest.ToolCallCount,
            manifest.PolicyDecisionCount,
            manifest.TraceEventCount,
            manifest.ModelCallCount,
            manifest.TotalModelPromptTokens,
            manifest.TotalModelCompletionTokens,
            manifest.TotalModelEstimatedCostUnits,
            manifest.TotalModelLatencyMs,
            manifest.PrimaryModelProviderName,
            manifest.PrimaryModelId,
            manifest.PrimaryPromptProfileRef,
            manifest.PrimaryModelProfileRef,
            manifest.ExternalAgentInvocationCompletedCount,
            manifest.ManifestVersion,
            manifest.ContentHash);
    }

    public static PolicyBundleSummaryDto ToSummaryDto(this PolicyBundle bundle) =>
        new(bundle.Id,
            bundle.Name,
            bundle.Version.ToString(),
            bundle.IsPublished,
            bundle.CreatedAt,
            bundle.PublishedAt);

    public static PolicyBundleDetailDto ToDetailDto(this PolicyBundle bundle) =>
        new(bundle.Id,
            bundle.Name,
            bundle.Version.ToString(),
            bundle.IsPublished,
            bundle.CreatedAt,
            bundle.PublishedAt,
            bundle.Rules.Select(r => r.ToDto()).ToList());

    public static PolicyRuleDto ToDto(this PolicyRule rule) =>
        new(rule.Id,
            rule.Kind,
            rule.Scope,
            rule.Effect,
            rule.TargetKey,
            rule.ThresholdValue,
            rule.Description,
            rule.ScopeTenantId,
            rule.ScopeWorkspaceId,
            rule.ScopeProjectId,
            rule.ScopeKnowledgeScopeId);

    public static ActivePolicyProfileDto ToDto(this ActivePolicyProfile active) =>
        new(active.ProfileId,
            active.ProfileName,
            active.BundleId,
            active.BundleVersion.ToString(),
            active.ActivatedAt);
}
