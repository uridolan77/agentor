namespace Agentor.Application.HumanReview;

/// <summary>
/// Raised when a human operator attempts to approve a run whose review workflow is escalated
/// without holding a governance approver (or system) role. Maps to HTTP 403 at the API boundary.
/// </summary>
public sealed class GovernanceApproverRequiredException : Exception
{
    public GovernanceApproverRequiredException()
        : base("Escalated human reviews require a governance approver role to approve.")
    {
    }
}
