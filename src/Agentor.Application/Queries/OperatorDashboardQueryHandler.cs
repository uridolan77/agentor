using Agentor.Application.Abstractions;
using Agentor.Contracts;
using Agentor.Domain.Enums;

namespace Agentor.Application.Queries;

public sealed class OperatorDashboardQueryHandler
{
    private const string DeferredRisksNote =
        "SCOPE-001: policy rule scope (tenant/workspace/project) is modeled but not enforced in evaluation — see docs/RELEASE/v1.0-RC-DEFERRED-ITEMS.md.";

    private readonly IManagementRecipeStore _recipes;
    private readonly IManagementPlanStore _plans;
    private readonly IManagementPolicyProfileStore _policyProfiles;
    private readonly ISkillPackageCatalog _skills;
    private readonly IAgentRunRepository _runs;
    private readonly IPolicyProfileRepository _policyProfileRepository;
    private readonly IIntegrationStatusReader _integrationStatus;
    private readonly IClock _clock;

    public OperatorDashboardQueryHandler(
        IManagementRecipeStore recipes,
        IManagementPlanStore plans,
        IManagementPolicyProfileStore policyProfiles,
        ISkillPackageCatalog skills,
        IAgentRunRepository runs,
        IPolicyProfileRepository policyProfileRepository,
        IIntegrationStatusReader integrationStatus,
        IClock clock)
    {
        _recipes = recipes;
        _plans = plans;
        _policyProfiles = policyProfiles;
        _skills = skills;
        _runs = runs;
        _policyProfileRepository = policyProfileRepository;
        _integrationStatus = integrationStatus;
        _clock = clock;
    }

    public async Task<OperatorDashboardResponseDto> HandleAsync(CancellationToken cancellationToken)
    {
        var reviewPage = await _runs.ListSummariesAsync(0, 1, cancellationToken, AgentRunStatus.RequiresReview);
        var failedPage = await _runs.ListSummariesAsync(0, 1, cancellationToken, AgentRunStatus.Failed);
        var integration = await _integrationStatus.GetStatusAsync(cancellationToken);
        var activeProfile = await _policyProfileRepository.GetActiveAsync(cancellationToken);

        var notReady = integration.Integrations.Values.Count(a => !a.Ready);

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
                    ["pendingHumanReviewCount"] = reviewPage.TotalCount.ToString(),
                    ["failedRunCount"] = failedPage.TotalCount.ToString()
                }),
            ["recipes"] = new OperatorDashboardModuleDto(
                "Recipes",
                new[]
                {
                    new OperatorDashboardModuleLinkDto("Recipes", "/api/v1/recipes")
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["registeredRecipes"] = _recipes.List().Count.ToString()
                }),
            ["queue"] = new OperatorDashboardModuleDto(
                "Queue",
                new[]
                {
                    new OperatorDashboardModuleLinkDto("Ops queue snapshot", "/api/v1/ops/queue")
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            ["outbox"] = new OperatorDashboardModuleDto(
                "Outbox",
                new[]
                {
                    new OperatorDashboardModuleLinkDto("Ops outbox snapshot", "/api/v1/ops/outbox")
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
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
                    new OperatorDashboardModuleLinkDto("Policy profiles (artifacts)", "/api/v1/policy-profiles"),
                    new OperatorDashboardModuleLinkDto("Policy bundles", "/api/v1/policy-bundles")
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["registeredPolicyProfiles"] = _policyProfiles.List().Count.ToString()
                }),
            ["policyRuntime"] = new OperatorDashboardModuleDto(
                "Active policy profile",
                new[]
                {
                    new OperatorDashboardModuleLinkDto("Activate profile (runtime)", "/api/v1/policy-profiles/{profileId}/activate")
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["hasActiveProfile"] = (activeProfile is not null).ToString(),
                    ["activeProfileId"] = activeProfile?.ProfileId.ToString("D") ?? "",
                    ["activeBundleVersion"] = activeProfile?.BundleVersion.ToString() ?? ""
                }),
            ["reviews"] = new OperatorDashboardModuleDto(
                "Reviews",
                new[]
                {
                    new OperatorDashboardModuleLinkDto("Pending inbox", "/api/v1/reviews/pending"),
                    new OperatorDashboardModuleLinkDto("Apply decision (alias)", "/api/v1/reviews/{runId}/decisions")
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["pendingHumanReviewCount"] = reviewPage.TotalCount.ToString()
                }),
            ["integrations"] = new OperatorDashboardModuleDto(
                "Integration readiness",
                new[]
                {
                    new OperatorDashboardModuleLinkDto("Integration status", "/api/v1/integrations/status"),
                    new OperatorDashboardModuleLinkDto("Readiness gate", "/ready")
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["integrationsReady"] = integration.Ready.ToString(),
                    ["integrationAdapterNotReadyCount"] = notReady.ToString()
                }),
            ["quality"] = new OperatorDashboardModuleDto(
                "Quality",
                new[]
                {
                    new OperatorDashboardModuleLinkDto("Audit export (legacy path)", "/api/v1/agent-runs/{runId}/audit-export"),
                    new OperatorDashboardModuleLinkDto("Audit packet (alias)", "/api/v1/runs/{runId}/audit-packet"),
                    new OperatorDashboardModuleLinkDto("Run manifest", "/api/v1/agent-runs/{runId}/manifest")
                },
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["failedRunCount"] = failedPage.TotalCount.ToString(),
                    ["pendingHumanReviewCount"] = reviewPage.TotalCount.ToString()
                }),
            ["deferredRisks"] = new OperatorDashboardModuleDto(
                "Deferred risks",
                Array.Empty<OperatorDashboardModuleLinkDto>(),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["note"] = DeferredRisksNote
                })
        };

        return new OperatorDashboardResponseDto(_clock.UtcNow, modules);
    }
}
