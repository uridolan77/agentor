using Agentor.Api.Mapping;
using Agentor.Api.Security;
using Agentor.Application.Abstractions;
using Agentor.Contracts;
using Agentor.Domain.Policy;
using Microsoft.AspNetCore.Http;
using Ontogony.Contracts.Events;

namespace Agentor.Api.Endpoints;

public static class PolicyBundleEndpoints
{
    public static RouteGroupBuilder MapPolicyBundleEndpoints(this RouteGroupBuilder v1)
    {
        MapBundles(v1);
        MapProfileActivation(v1);
        return v1;
    }

    private static void MapBundles(RouteGroupBuilder v1)
    {
        v1.MapGet("/policy-bundles", async (
                IPolicyBundleRepository repo,
                ICurrentActorAccessor actorAccessor,
                IAuthorizationDecisionService authorization,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var authResult = EndpointAuthorization.Require(
                    httpContext,
                    actorAccessor,
                    authorization,
                    AgentorPermission.PolicyBundleRead);
                if (authResult is not null)
                {
                    return authResult;
                }

                var bundles = await repo.ListAsync(cancellationToken);
                return Results.Ok(new PolicyBundleListDto(
                    bundles.Select(b => b.ToSummaryDto()).ToList()));
            })
            .WithName("ListPolicyBundles")
            .WithTags("Policy")
            .WithSummary("Lists all policy bundles (published and draft). Does not activate any bundle.");

        v1.MapPost("/policy-bundles", async (
                CreatePolicyBundleRequestDto body,
                IPolicyBundleRepository repo,
                IClock clock,
                ICurrentActorAccessor actorAccessor,
                IAuthorizationDecisionService authorization,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var authResult = EndpointAuthorization.Require(
                    httpContext,
                    actorAccessor,
                    authorization,
                    AgentorPermission.PolicyBundleWrite);
                if (authResult is not null)
                {
                    return authResult;
                }

                var traceId = httpContext.Response.Headers[OntogonyEventHeaders.TraceId].ToString();

                if (string.IsNullOrWhiteSpace(body.Name))
                {
                    return Results.BadRequest(new ApiErrorDto(
                        "ValidationError", "Bundle name is required.", traceId, ["Name is required."]));
                }

                if (!PolicyBundleVersion.TryParse(body.Version, out var version) || version is null)
                {
                    return Results.BadRequest(new ApiErrorDto(
                        "ValidationError",
                        $"Invalid bundle version '{body.Version}'. Expected 'major.minor' (e.g. '1.0').",
                        traceId,
                        [$"Version '{body.Version}' is not in 'major.minor' format."]));
                }

                var now = clock.UtcNow;
                PolicyBundle bundle;
                try
                {
                    bundle = PolicyBundle.Create(
                        Guid.NewGuid(),
                        body.Name,
                        version,
                        (body.Rules ?? []).Select((r, idx) => new PolicyRule(
                            Guid.NewGuid(),
                            r.Kind,
                            r.Scope,
                            r.Effect,
                            r.TargetKey,
                            r.ThresholdValue,
                            string.IsNullOrWhiteSpace(r.Description) ? $"Rule {idx + 1}" : r.Description,
                            r.ScopeTenantId,
                            r.ScopeWorkspaceId,
                            r.ScopeProjectId,
                            r.ScopeKnowledgeScopeId)),
                        now);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new ApiErrorDto(
                        "ValidationError", ex.Message, traceId, [ex.Message]));
                }

                // Bundles created via the API are immediately published (no separate publish step).
                bundle.Publish(now);
                await repo.SaveAsync(bundle, cancellationToken);

                return Results.Created(
                    $"/api/v1/policy-bundles/{bundle.Id:D}",
                    bundle.ToDetailDto());
            })
            .WithName("CreatePolicyBundle")
            .WithTags("Policy")
            .WithSummary("Creates and publishes a versioned policy bundle. Creating a bundle does NOT activate it.");

        v1.MapGet("/policy-bundles/{id:guid}", async (
                Guid id,
                IPolicyBundleRepository repo,
                ICurrentActorAccessor actorAccessor,
                IAuthorizationDecisionService authorization,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var authResult = EndpointAuthorization.Require(
                    httpContext,
                    actorAccessor,
                    authorization,
                    AgentorPermission.PolicyBundleRead);
                if (authResult is not null)
                {
                    return authResult;
                }

                var bundle = await repo.GetAsync(id, cancellationToken);
                if (bundle is null)
                {
                    var traceId = httpContext.Response.Headers[OntogonyEventHeaders.TraceId].ToString();
                    return Results.NotFound(new ApiErrorDto(
                        "BundleNotFound", $"Policy bundle '{id}' was not found.", traceId));
                }

                return Results.Ok(bundle.ToDetailDto());
            })
            .WithName("GetPolicyBundle")
            .WithTags("Policy")
            .WithSummary("Returns the full policy bundle including all rules.");
    }

    private static void MapProfileActivation(RouteGroupBuilder v1)
    {
        v1.MapPost("/policy-profiles/{id:guid}/activate", async (
                Guid id,
                ActivatePolicyProfileRequestDto body,
                IManagementPolicyProfileStore profileStore,
                IPolicyBundleRepository bundleRepo,
                IPolicyProfileRepository profileRepo,
                ICurrentActorAccessor actorAccessor,
                IAuthorizationDecisionService authorization,
                IClock clock,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var authResult = EndpointAuthorization.Require(
                    httpContext,
                    actorAccessor,
                    authorization,
                    AgentorPermission.PolicyBundleWrite);
                if (authResult is not null)
                {
                    return authResult;
                }

                var traceId = httpContext.Response.Headers[OntogonyEventHeaders.TraceId].ToString();

                // Look up the management profile by ID (existing flat store, backward-compatible).
                var managedProfile = profileStore.Get(id);
                if (managedProfile is null)
                {
                    return Results.NotFound(new ApiErrorDto(
                        "ProfileNotFound",
                        $"Policy profile '{id}' was not found. Create it via POST /api/v1/policy-profiles first.",
                        traceId));
                }

                if (!PolicyBundleVersion.TryParse(body.BundleVersion, out var bundleVersion) || bundleVersion is null)
                {
                    return Results.BadRequest(new ApiErrorDto(
                        "ValidationError",
                        $"Invalid bundle version '{body.BundleVersion}'. Expected 'major.minor'.",
                        traceId,
                        [$"BundleVersion '{body.BundleVersion}' is not in 'major.minor' format."]));
                }

                var bundle = await bundleRepo.GetAsync(body.BundleId, cancellationToken);
                if (bundle is null)
                {
                    return Results.NotFound(new ApiErrorDto(
                        "BundleNotFound",
                        $"Policy bundle '{body.BundleId}' was not found.",
                        traceId));
                }

                if (!bundle.IsPublished)
                {
                    return Results.Conflict(new ApiErrorDto(
                        "BundleNotPublished",
                        $"Bundle '{body.BundleId}' has not been published and cannot be activated.",
                        traceId));
                }

                if (bundle.Version != bundleVersion)
                {
                    return Results.Conflict(new ApiErrorDto(
                        "BundleVersionMismatch",
                        $"Bundle '{body.BundleId}' is version {bundle.Version} but request specified {bundleVersion}.",
                        traceId));
                }

                var now = clock.UtcNow;
                var actorId = actorAccessor.Current.ActorId;

                var active = new ActivePolicyProfile(
                    managedProfile.Id,
                    managedProfile.Name,
                    bundle.Id,
                    bundle.Version,
                    now,
                    actorId);

                await profileRepo.SetActiveAsync(active, cancellationToken);

                return Results.Ok(active.ToDto());
            })
            .WithName("ActivatePolicyProfile")
            .WithTags("Policy")
            .WithSummary("Activates a policy profile bound to a specific published bundle version. Explicit activation is audited.");
    }
}
