using Agentor.Api.Mapping;
using Agentor.Api.Security;
using Agentor.Application.Commands;
using Agentor.Application.Management;
using Agentor.Application.Queries;
using Agentor.Application.Abstractions;
using Agentor.Contracts;
using Agentor.Domain;

namespace Agentor.Api;

internal static class Phase13ProductEndpoints
{
    public static void MapProductSurface(RouteGroupBuilder v1)
    {
        MapRecipes(v1);
        MapPlans(v1);
        MapSkills(v1);
        MapPolicyProfiles(v1);
        MapRunAliases(v1);
        MapOperator(v1);
        MapReviews(v1);
    }

    private static void MapRecipes(RouteGroupBuilder v1)
    {
        v1.MapGet("/recipes", (IManagementRecipeStore store) =>
            Results.Ok(store.List().Select(r => r.ToResponse()).ToList()))
            .WithName("ListRecipes")
            .WithTags("Management")
            .WithSummary("Lists declarative recipes registered in the operator artifact store (no execution).");

        v1.MapGet("/recipes/{recipeId:guid}", (Guid recipeId, IManagementRecipeStore store) =>
        {
            var r = store.Get(recipeId);
            return r is null ? Results.NotFound() : Results.Ok(r.ToResponse());
        })
            .WithName("GetRecipe")
            .WithTags("Management");

        v1.MapPost("/recipes", (
                CreateRecipeRequestDto body,
                IManagementRecipeStore store,
                HttpContext httpContext) =>
            {
                var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
                var id = Guid.NewGuid();
                if (!ManagementArtifactMapper.TryMap(body, id, out var recipe, out var validation) || recipe is null)
                {
                    return Results.BadRequest(new ApiErrorDto(
                        "ValidationError",
                        "Recipe validation failed.",
                        traceId,
                        validation.Issues.Select(i => string.IsNullOrEmpty(i.StepId) ? $"{i.Code}: {i.Message}" : $"{i.Code} ({i.StepId}): {i.Message}").ToList()));
                }

                if (!store.TryAdd(recipe))
                {
                    return Results.Conflict(new ApiErrorDto("RecipeConflict", "A recipe with this id already exists.", traceId, null));
                }

                return Results.Created($"/api/v1/recipes/{recipe.Id:D}", recipe.ToResponse());
            })
            .WithName("CreateRecipe")
            .WithTags("Management")
            .WithSummary("Registers a validated recipe. Does not execute coordination.");
    }

    private static void MapPlans(RouteGroupBuilder v1)
    {
        v1.MapGet("/plans", (IManagementPlanStore store) =>
            Results.Ok(store.List().Select(p => p.ToResponse()).ToList()))
            .WithName("ListPlans")
            .WithTags("Management");

        v1.MapGet("/plans/{planId:guid}", (Guid planId, IManagementPlanStore store) =>
        {
            var p = store.Get(planId);
            return p is null ? Results.NotFound() : Results.Ok(p.ToResponse());
        })
            .WithName("GetPlan")
            .WithTags("Management");

        v1.MapPost("/plans", (
                CreatePlanFromRecipeRequestDto body,
                IManagementRecipeStore recipes,
                IManagementPlanStore plans,
                IClock clock,
                HttpContext httpContext) =>
            {
                var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
                var recipe = recipes.Get(body.RecipeId);
                if (recipe is null)
                {
                    return Results.NotFound(new ApiErrorDto(
                        "RecipeNotFound",
                        $"Recipe '{body.RecipeId}' was not found in the artifact store.",
                        traceId,
                        null));
                }

                var planId = body.PlanId ?? Guid.NewGuid();
                AgentPlan plan;
                try
                {
                    plan = AgentPlan.Instantiate(recipe, planId, clock.UtcNow);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new ApiErrorDto("ValidationError", ex.Message, traceId, [ex.Message]));
                }

                if (!plans.TryAdd(plan))
                {
                    return Results.Conflict(new ApiErrorDto("PlanConflict", "A plan with this id already exists.", traceId, null));
                }

                return Results.Created($"/api/v1/plans/{plan.Id:D}", plan.ToResponse());
            })
            .WithName("CreatePlanFromRecipe")
            .WithTags("Management")
            .WithSummary("Instantiates a plan from a stored recipe. Does not execute the plan.");
    }

    private static void MapSkills(RouteGroupBuilder v1)
    {
        v1.MapGet("/skills", (ISkillPackageCatalog catalog) =>
            Results.Ok(catalog.ListRegisteredPackages()
                .Select(p => new SkillPackageSummaryResponseDto(p.Id, p.SkillKey, p.Version.Value, p.Name))
                .ToList()))
            .WithName("ListSkillPackages")
            .WithTags("Management");

        v1.MapGet("/skills/{skillKey}/{version}", (string skillKey, string version, ISkillPackageCatalog catalog) =>
        {
            if (!catalog.TryGet(skillKey, AgentRecipeVersion.Parse(version), out var pkg) || pkg is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(pkg.ToDetailResponse());
        })
            .WithName("GetSkillPackage")
            .WithTags("Management");

        v1.MapPost("/skills", (
                CreateSkillPackageRequestDto body,
                ISkillPackageCatalog catalog,
                HttpContext httpContext) =>
            {
                var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
                var version = AgentRecipeVersion.Parse(body.Version);
                if (catalog.TryGet(body.SkillKey, version, out var existing) && existing is not null)
                {
                    return Results.Conflict(new ApiErrorDto(
                        "SkillPackageConflict",
                        $"Skill '{body.SkillKey}' version '{body.Version}' is already registered.",
                        traceId,
                        null));
                }

                if (!ManagementArtifactMapper.TryMap(body, Guid.NewGuid(), out var package, out var validation) || package is null)
                {
                    return Results.BadRequest(new ApiErrorDto(
                        "ValidationError",
                        "Skill package validation failed.",
                        traceId,
                        validation.Issues.Select(i => $"{i.Code}: {i.Message}").ToList()));
                }

                catalog.RegisterPackage(package);
                return Results.Created(
                    $"/api/v1/skills/{Uri.EscapeDataString(package.SkillKey)}/{Uri.EscapeDataString(package.Version.Value)}",
                    package.ToDetailResponse());
            })
            .WithName("CreateSkillPackage")
            .WithTags("Management")
            .WithSummary("Registers a skill package in the catalog. Does not invoke procedures.");
    }

    private static void MapPolicyProfiles(RouteGroupBuilder v1)
    {
        v1.MapGet("/policy-profiles", (IManagementPolicyProfileStore store) =>
            Results.Ok(store.List().Select(p => new PolicyProfileArtifactResponseDto(p.Id, p.Name, p.Rules, p.CreatedAt)).ToList()))
            .WithName("ListPolicyProfiles")
            .WithTags("Management");

        v1.MapGet("/policy-profiles/{profileId:guid}", (Guid profileId, IManagementPolicyProfileStore store) =>
        {
            var p = store.Get(profileId);
            return p is null
                ? Results.NotFound()
                : Results.Ok(new PolicyProfileArtifactResponseDto(p.Id, p.Name, p.Rules, p.CreatedAt));
        })
            .WithName("GetPolicyProfile")
            .WithTags("Management");

        v1.MapPost("/policy-profiles", (
                CreatePolicyProfileRequestDto body,
                IManagementPolicyProfileStore store,
                HttpContext httpContext) =>
            {
                var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
                if (string.IsNullOrWhiteSpace(body.Name))
                {
                    return Results.BadRequest(new ApiErrorDto("ValidationError", "Name is required.", traceId, ["Name is required."]));
                }

                var created = store.Add(body.Name, body.Rules);
                return Results.Created($"/api/v1/policy-profiles/{created.Id:D}", new PolicyProfileArtifactResponseDto(created.Id, created.Name, created.Rules, created.CreatedAt));
            })
            .WithName("CreatePolicyProfile")
            .WithTags("Management")
            .WithSummary("Stores a policy profile artifact for operators (does not change runtime ActiveProfile).");
    }

    private static void MapRunAliases(RouteGroupBuilder v1)
    {
        v1.MapGet("/runs/{runId:guid}/timeline", async (
                Guid runId,
                GetRunTimelineQueryHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var dto = await handler.HandleAsync(runId, cancellationToken);
                if (dto is null)
                {
                    var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
                    return Results.NotFound(new ApiErrorDto("RunNotFound", $"Run '{runId}' was not found.", traceId));
                }

                return Results.Ok(dto);
            })
            .WithName("GetRunTimeline")
            .WithTags("Runs")
            .WithSummary("Deterministic ordered trace; skill invocation spans plus timeline v2 groups (plan steps, skill blocks, policy decisions, review decisions). Indices reference orderedEvents.");

        v1.MapGet("/runs/{runId:guid}/coordination-view", async (
                Guid runId,
                GetRunCoordinationViewQueryHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var dto = await handler.HandleAsync(runId, cancellationToken);
                if (dto is null)
                {
                    var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
                    return Results.NotFound(new ApiErrorDto("RunNotFound", $"Run '{runId}' was not found.", traceId));
                }

                return Results.Ok(dto);
            })
            .WithName("GetRunCoordinationView")
            .WithTags("Runs");

        v1.MapGet("/runs/{runId:guid}/audit-packet", async (
                Guid runId,
                string? format,
                GetRunAuditExportQueryHandler handler,
                ICurrentActorAccessor actorAccessor,
                IAuthorizationDecisionService authorization,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var authResult = EndpointAuthorization.Require(
                    httpContext,
                    actorAccessor,
                    authorization,
                    AgentorPermission.AuditRead);
                if (authResult is not null)
                {
                    return authResult;
                }

                var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
                if (!AuditExportFormatParser.TryParse(format, out var exportFormat, out var formatError))
                {
                    return Results.BadRequest(new ApiErrorDto(
                        "AuditExportFormatInvalid",
                        formatError ?? "Invalid audit export format.",
                        traceId,
                        formatError is null ? null : [formatError]));
                }

                var result = await handler.HandleAsync(runId, exportFormat, cancellationToken);
                if (result is null)
                {
                    return Results.NotFound(new ApiErrorDto("RunNotFound", $"Run '{runId}' was not found.", traceId));
                }

                httpContext.Response.Headers["X-Agentor-Audit-Content-SHA256"] = result.ContentSha256Hex;
                return Results.Text(result.ResponseBody, "application/json; charset=utf-8");
            })
            .WithName("GetRunAuditPacket")
            .WithTags("Runs")
            .WithSummary("Deterministic JSON audit views for a run (canonical minified default). Query format=canonical|pretty|redactionReport|hashOnly; SHA-256 header always matches canonical audit JSON.");
    }

    private static void MapOperator(RouteGroupBuilder v1)
    {
        v1.MapGet("/operator/dashboard", async (
                OperatorDashboardQueryHandler handler,
                ICurrentActorAccessor actorAccessor,
                IAuthorizationDecisionService authorization,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var authResult = EndpointAuthorization.Require(
                    httpContext,
                    actorAccessor,
                    authorization,
                    AgentorPermission.OpsRead);
                if (authResult is not null)
                {
                    return authResult;
                }

                return Results.Ok(await handler.HandleAsync(cancellationToken));
            })
            .WithName("GetOperatorDashboard")
            .WithTags("Operator")
            .WithSummary("Read-only dashboard DTO (requires OpsRead): links and operational aggregates; same permission family as /api/v1/ops/*.");
    }

    private static void MapReviews(RouteGroupBuilder v1)
    {
        v1.MapGet("/reviews/pending", async (
                int? skip,
                int? take,
                ICurrentActorAccessor actorAccessor,
                IAuthorizationDecisionService authorization,
                HttpContext httpContext,
                ListPendingHumanReviewsQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var authResult = EndpointAuthorization.Require(
                    httpContext,
                    actorAccessor,
                    authorization,
                    AgentorPermission.GovernanceReviewRead);
                if (authResult is not null)
                {
                    return authResult;
                }

                return Results.Ok(await handler.HandleAsync(skip ?? 0, take ?? 50, cancellationToken));
            })
            .WithName("ListPendingHumanReviews")
            .WithTags("Reviews")
            .WithSummary("Lists runs in RequiresReview (inbox).");

        v1.MapPost("/reviews/{runId:guid}/decisions", async (
                Guid runId,
                ApplyHumanReviewRequestDto request,
                ApplyHumanReviewDecisionHandler handler,
                ICurrentActorAccessor actorAccessor,
                IAuthorizationDecisionService authorization,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var authResult = EndpointAuthorization.Require(
                    httpContext,
                    actorAccessor,
                    authorization,
                    AgentorPermission.GovernanceReviewWrite);
                if (authResult is not null)
                {
                    return authResult;
                }

                var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();
                try
                {
                    var run = await handler.HandleAsync(
                        new ApplyHumanReviewDecisionCommand(runId, request.Kind, request.Note, request.RelatedPriorActorId),
                        cancellationToken);

                    if (run is null)
                    {
                        return Results.NotFound(new ApiErrorDto("RunNotFound", $"Agent run '{runId}' was not found.", traceId));
                    }

                    return Results.Ok(run.ToDto());
                }
                catch (InvalidOperationException ex)
                {
                    return Results.Conflict(new ApiErrorDto("HumanReviewInvalid", ex.Message, traceId, [ex.Message]));
                }
            })
            .WithName("ApplyReviewDecision")
            .WithTags("Reviews")
            .WithSummary("Alias of POST /agent-runs/{id}/human-review; requires actor context (X-Agentor-Actor-Id).");
    }
}
