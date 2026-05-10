using Agentor.Application.Abstractions;
using Agentor.Contracts;

namespace Agentor.Api.Security;

public static class EndpointAuthorization
{
    public static IResult? Require(
        HttpContext httpContext,
        ICurrentActorAccessor actorAccessor,
        IAuthorizationDecisionService authorization,
        AgentorPermission permission)
    {
        var traceId = httpContext.Response.Headers["X-Agentor-Trace-Id"].ToString();

        ActorContext actor;
        try
        {
            actor = actorAccessor.Current;
        }
        catch (InvalidOperationException ex)
        {
            return Results.Json(
                new ApiErrorDto("Unauthorized", ex.Message, traceId),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var decision = authorization.Authorize(actor, permission);
        if (decision.Allowed)
        {
            return null;
        }

        return Results.Json(
            new ApiErrorDto("Forbidden", decision.Reason ?? "Permission denied.", traceId),
            statusCode: StatusCodes.Status403Forbidden);
    }
}
