using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Contracts;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;
using Agentor.Domain.Policy;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Agentor.Api.Tests;

/// <summary>
/// Table-driven checks aligned with <c>docs/security/AUTHORIZATION_MATRIX.md</c> (Phase 38 / PR155).
/// </summary>
public sealed class AuthorizationMatrixApiTests : IClassFixture<AuthorizationMatrixApiFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly AuthorizationMatrixApiFixture _fixture;

    public AuthorizationMatrixApiTests(AuthorizationMatrixApiFixture fixture) => _fixture = fixture;

    private void SetActor(ActorRole role, Guid? actorId = null)
    {
        _fixture.ActorAccessor.ThrowOnAccess = false;
        var id = actorId ?? Guid.NewGuid();
        var name = role.ToString();
        _fixture.ActorAccessor.CurrentActor = new ActorContext(id, name, role);
    }

    [Theory]
    [InlineData("post-agent-runs")]
    [InlineData("post-queued")]
    [InlineData("post-athanor-evidence")]
    [InlineData("post-athanor-candidates")]
    [InlineData("post-athanor-review-queue")]
    [InlineData("post-human-review")]
    [InlineData("get-integrations-status")]
    [InlineData("get-ops-queue")]
    [InlineData("get-ops-outbox")]
    [InlineData("get-ops-leases")]
    [InlineData("get-ops-diagnostics")]
    [InlineData("get-operator-dashboard")]
    [InlineData("post-policy-bundles")]
    [InlineData("post-policy-activate")]
    [InlineData("post-recipes")]
    [InlineData("post-plans")]
    [InlineData("post-skills")]
    [InlineData("post-policy-profiles")]
    [InlineData("post-reviews-decisions")]
    public async Task Service_role_is_forbidden_on_privileged_routes(string routeKey)
    {
        SetActor(ActorRole.Service);
        using var client = _fixture.CreateClient();
        var response = await InvokeServiceForbiddenRoute(routeKey, client);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<HttpResponseMessage> InvokeServiceForbiddenRoute(string key, HttpClient client)
    {
        var rid = _fixture.CompletedRunId;
        var reviewId = _fixture.ReviewRunId;
        return key switch
        {
            "post-agent-runs" => await client.PostAsJsonAsync(
                "/api/v1/agent-runs",
                new StartAgentRunRequestDto("svc-blocked", "objective"),
                JsonOptions),
            "post-queued" => await client.PostAsJsonAsync(
                "/api/v1/agent-runs/queued",
                new StartAgentRunRequestDto("svc-queued", "objective"),
                JsonOptions),
            "post-athanor-evidence" => await client.PostAsJsonAsync(
                $"/api/v1/agent-runs/{rid:D}/athanor/evidence-provenance",
                new AttachEvidenceProvenanceRequestDto("q"),
                JsonOptions),
            "post-athanor-candidates" => await client.PostAsJsonAsync(
                $"/api/v1/agent-runs/{rid:D}/athanor/candidates",
                new SubmitAthanorCandidateRequestDto("s", "{}"),
                JsonOptions),
            "post-athanor-review-queue" => await client.PostAsJsonAsync(
                $"/api/v1/agent-runs/{rid:D}/athanor/review-queue",
                new QueueAthanorReviewRequestDto(Guid.NewGuid()),
                JsonOptions),
            "post-human-review" => await client.PostAsJsonAsync(
                $"/api/v1/agent-runs/{reviewId:D}/human-review",
                new ApplyHumanReviewRequestDto(ReviewDecisionKind.Approve),
                JsonOptions),
            "get-integrations-status" => await client.GetAsync("/api/v1/integrations/status"),
            "get-ops-queue" => await client.GetAsync("/api/v1/ops/queue"),
            "get-ops-outbox" => await client.GetAsync("/api/v1/ops/outbox"),
            "get-ops-leases" => await client.GetAsync("/api/v1/ops/leases"),
            "get-ops-diagnostics" => await client.GetAsync("/api/v1/ops/diagnostics-report"),
            "get-operator-dashboard" => await client.GetAsync("/api/v1/operator/dashboard"),
            "post-policy-bundles" => await client.PostAsJsonAsync(
                "/api/v1/policy-bundles",
                new CreatePolicyBundleRequestDto(
                    "svc-block",
                    "1.0",
                    [
                        new CreatePolicyRuleDto(
                            PolicyRuleKind.ToolAccess,
                            PolicyRuleScope.Global,
                            PolicyRuleEffect.Allow,
                            WellKnownToolKeys.Pr1FakeTool,
                            null,
                            "r"),
                    ]),
                JsonOptions),
            "post-policy-activate" => await client.PostAsJsonAsync(
                $"/api/v1/policy-profiles/{Guid.NewGuid():D}/activate",
                new ActivatePolicyProfileRequestDto(Guid.NewGuid(), "1.0"),
                JsonOptions),
            "post-recipes" => await client.PostAsJsonAsync(
                "/api/v1/recipes",
                new CreateRecipeRequestDto(
                    "svc-recipe",
                    "1.0.0",
                    CoordinationTopology.SequentialPipeline,
                    [new RecipeStepRequestDto("s1", 0, RecipeStepKind.Tool, WellKnownToolKeys.Pr1FakeTool)],
                    FailureHandlingPolicy.FailFast,
                    null),
                JsonOptions),
            "post-plans" => await client.PostAsJsonAsync(
                "/api/v1/plans",
                new CreatePlanFromRecipeRequestDto(Guid.NewGuid(), null),
                JsonOptions),
            "post-skills" => await client.PostAsJsonAsync(
                "/api/v1/skills",
                new CreateSkillPackageRequestDto(
                    "svc.skill",
                    "1.0.0",
                    "n",
                    "p",
                    [new SkillProcedureStepRequestDto("p1", 0, "seg", SkillProcedureStepKind.Segment)]),
                JsonOptions),
            "post-policy-profiles" => await client.PostAsJsonAsync(
                "/api/v1/policy-profiles",
                new CreatePolicyProfileRequestDto("svc-profile", new PolicyProfileRulesDto()),
                JsonOptions),
            "post-reviews-decisions" => await client.PostAsJsonAsync(
                $"/api/v1/reviews/{reviewId:D}/decisions",
                new ApplyHumanReviewRequestDto(ReviewDecisionKind.Approve),
                JsonOptions),
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, null),
        };
    }

    [Theory]
    [InlineData("get-agent-runs")]
    [InlineData("get-agent-run-by-id")]
    [InlineData("get-trace")]
    [InlineData("get-steps")]
    [InlineData("get-tool-calls")]
    [InlineData("get-manifest")]
    [InlineData("get-audit-export")]
    [InlineData("get-queued-status")]
    [InlineData("get-athanor-latest")]
    [InlineData("get-athanor-canonical")]
    [InlineData("get-policy-bundles")]
    [InlineData("get-recipes")]
    [InlineData("get-plans")]
    [InlineData("get-skills")]
    [InlineData("get-policy-profiles")]
    [InlineData("get-timeline")]
    [InlineData("get-coordination-view")]
    [InlineData("get-audit-packet")]
    [InlineData("get-reviews-pending")]
    public async Task Service_role_is_allowed_on_read_routes(string routeKey)
    {
        SetActor(ActorRole.Service);
        using var client = _fixture.CreateClient();
        var response = await InvokeServiceReadRoute(routeKey, client);
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound or HttpStatusCode.Accepted,
            $"Expected success or not-found, got {response.StatusCode} for {routeKey}");
    }

    private async Task<HttpResponseMessage> InvokeServiceReadRoute(string key, HttpClient client)
    {
        var rid = _fixture.CompletedRunId;
        return key switch
        {
            "get-agent-runs" => await client.GetAsync("/api/v1/agent-runs"),
            "get-agent-run-by-id" => await client.GetAsync($"/api/v1/agent-runs/{rid:D}"),
            "get-trace" => await client.GetAsync($"/api/v1/agent-runs/{rid:D}/trace"),
            "get-steps" => await client.GetAsync($"/api/v1/agent-runs/{rid:D}/steps"),
            "get-tool-calls" => await client.GetAsync($"/api/v1/agent-runs/{rid:D}/tool-calls"),
            "get-manifest" => await client.GetAsync($"/api/v1/agent-runs/{rid:D}/manifest"),
            "get-audit-export" => await client.GetAsync($"/api/v1/agent-runs/{rid:D}/audit-export"),
            "get-queued-status" => await client.GetAsync($"/api/v1/agent-runs/queued/{Guid.NewGuid():D}"),
            "get-athanor-latest" => await client.GetAsync($"/api/v1/agent-runs/{rid:D}/athanor/latest-snapshot"),
            "get-athanor-canonical" => await client.GetAsync(
                $"/api/v1/agent-runs/{rid:D}/athanor/canonical?key=matrix-test-key"),
            "get-policy-bundles" => await client.GetAsync("/api/v1/policy-bundles"),
            "get-recipes" => await client.GetAsync("/api/v1/recipes"),
            "get-plans" => await client.GetAsync("/api/v1/plans"),
            "get-skills" => await client.GetAsync("/api/v1/skills"),
            "get-policy-profiles" => await client.GetAsync("/api/v1/policy-profiles"),
            "get-timeline" => await client.GetAsync($"/api/v1/runs/{rid:D}/timeline"),
            "get-coordination-view" => await client.GetAsync($"/api/v1/runs/{rid:D}/coordination-view"),
            "get-audit-packet" => await client.GetAsync($"/api/v1/runs/{rid:D}/audit-packet"),
            "get-reviews-pending" => await client.GetAsync("/api/v1/reviews/pending?skip=0&take=5"),
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, null),
        };
    }

    [Fact]
    public async Task HumanGovernanceApprover_can_read_ops_diagnostics_like_operator()
    {
        SetActor(ActorRole.HumanGovernanceApprover);
        using var client = _fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/ops/diagnostics-report");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task System_role_can_read_ops_queue()
    {
        SetActor(ActorRole.System);
        using var client = _fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/ops/queue");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

}
