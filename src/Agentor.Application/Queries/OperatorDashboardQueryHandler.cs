using Agentor.Application.Abstractions;
using Agentor.Contracts;
using Agentor.Domain.Enums;

namespace Agentor.Application.Queries;

public sealed class OperatorDashboardQueryHandler
{
    private readonly IManagementRecipeStore _recipes;
    private readonly IManagementPlanStore _plans;
    private readonly IManagementPolicyProfileStore _policyProfiles;
    private readonly ISkillPackageCatalog _skills;
    private readonly IAgentRunRepository _runs;
    private readonly IClock _clock;

    public OperatorDashboardQueryHandler(
        IManagementRecipeStore recipes,
        IManagementPlanStore plans,
        IManagementPolicyProfileStore policyProfiles,
        ISkillPackageCatalog skills,
        IAgentRunRepository runs,
        IClock clock)
    {
        _recipes = recipes;
        _plans = plans;
        _policyProfiles = policyProfiles;
        _skills = skills;
        _runs = runs;
        _clock = clock;
    }

    public async Task<OperatorDashboardResponseDto> HandleAsync(CancellationToken cancellationToken)
    {
        var reviewPage = await _runs.ListSummariesAsync(0, 1, cancellationToken, AgentRunStatus.RequiresReview);

        var modules = new Dictionary<string, OperatorDashboardModuleDto>(StringComparer.OrdinalIgnoreCase)
        {
            ["runs"] = new OperatorDashboardModuleDto(
                "Runs",
                new[]
                {
                    new OperatorDashboardModuleLinkDto("List runs", "/api/v1/agent-runs"),
                    new OperatorDashboardModuleLinkDto("Run timeline (alias)", "/api/v1/runs/{runId}/timeline")
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["pendingHumanReviewCount"] = reviewPage.TotalCount.ToString()
                }),
            ["plans"] = new OperatorDashboardModuleDto(
                "Plans",
                new[]
                {
                    new OperatorDashboardModuleLinkDto("Artifact plans", "/api/v1/plans")
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["registeredArtifactPlans"] = _plans.List().Count.ToString()
                }),
            ["skills"] = new OperatorDashboardModuleDto(
                "Skills",
                new[]
                {
                    new OperatorDashboardModuleLinkDto("Skill packages", "/api/v1/skills")
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["registeredSkillPackages"] = _skills.ListRegisteredPackages().Count.ToString()
                }),
            ["policies"] = new OperatorDashboardModuleDto(
                "Policies",
                new[]
                {
                    new OperatorDashboardModuleLinkDto("Policy profiles (artifacts)", "/api/v1/policy-profiles")
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["registeredPolicyProfiles"] = _policyProfiles.List().Count.ToString()
                }),
            ["reviews"] = new OperatorDashboardModuleDto(
                "Reviews",
                new[]
                {
                    new OperatorDashboardModuleLinkDto("Pending inbox", "/api/v1/reviews/pending"),
                    new OperatorDashboardModuleLinkDto("Apply decision (governance path)", "/api/v1/reviews/{runId}/decisions")
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["pendingHumanReviewCount"] = reviewPage.TotalCount.ToString()
                }),
            ["integrations"] = new OperatorDashboardModuleDto(
                "Integrations",
                new[]
                {
                    new OperatorDashboardModuleLinkDto("Integration status", "/api/v1/integrations/status")
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            ["quality"] = new OperatorDashboardModuleDto(
                "Quality",
                new[]
                {
                    new OperatorDashboardModuleLinkDto("Audit export (legacy path)", "/api/v1/agent-runs/{runId}/audit-export"),
                    new OperatorDashboardModuleLinkDto("Audit packet (alias)", "/api/v1/runs/{runId}/audit-packet")
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
        };

        return new OperatorDashboardResponseDto(_clock.UtcNow, modules);
    }
}
