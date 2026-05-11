using Agentor.Application.Abstractions;
using Agentor.Application.Commands;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;

namespace Agentor.Application.HumanReview;

/// <summary>
/// Validates human-review commands against the current run and applies the decision to the aggregate.
/// </summary>
public sealed class HumanReviewDecisionApplicator(IClock clock, ICurrentActorAccessor actorAccessor)
{
    public void Apply(AgentRun run, ApplyHumanReviewDecisionCommand command)
    {
        if (run.Status != AgentRunStatus.RequiresReview)
        {
            throw new InvalidOperationException(
                $"Human review decisions apply only while the run requires review. Current status: {run.Status}.");
        }

        if (command.Kind == ReviewDecisionKind.RequestChanges && string.IsNullOrWhiteSpace(command.Note))
        {
            throw new InvalidOperationException("RequestChanges requires a note describing the requested changes.");
        }

        if (command.Kind == ReviewDecisionKind.Escalate && string.IsNullOrWhiteSpace(command.Note))
        {
            throw new InvalidOperationException("Escalate requires a note with the escalation reason.");
        }

        if (command.Kind == ReviewDecisionKind.Approve
            && run.ReviewWorkflowStatus == HumanReviewWorkflowStatus.Escalated)
        {
            var role = actorAccessor.Current.Role;
            if (role is not ActorRole.HumanGovernanceApprover and not ActorRole.System)
            {
                throw new InvalidOperationException(
                    "Escalated human reviews require a governance approver role to approve.");
            }
        }

        var actorId = actorAccessor.Current.ActorId;
        if (actorId == Guid.Empty)
        {
            throw new InvalidOperationException("Actor id is required for human review decisions.");
        }

        var resolution = command.Kind switch
        {
            ReviewDecisionKind.Approve => ReviewResolutionStatus.ResolvedApproved,
            ReviewDecisionKind.Reject => ReviewResolutionStatus.ResolvedRejected,
            ReviewDecisionKind.RequestChanges => ReviewResolutionStatus.ChangesRequested,
            ReviewDecisionKind.Escalate => ReviewResolutionStatus.Escalated,
            _ => ReviewResolutionStatus.Pending
        };

        var decision = new HumanReviewDecision(
            Guid.NewGuid(),
            command.Kind,
            actorId,
            clock.UtcNow,
            command.Note,
            resolution,
            command.RelatedPriorActorId);

        run.ApplyHumanReviewDecision(decision, clock.UtcNow);
    }
}
