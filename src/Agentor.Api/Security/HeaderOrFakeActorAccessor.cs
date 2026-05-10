using Agentor.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Agentor.Api.Security;

public sealed class HeaderOrFakeActorAccessor(
    IHttpContextAccessor http,
    IOptions<AgentorAuthOptions> authOptions) : ICurrentActorAccessor
{
    private static readonly Guid FallbackActorId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    public ActorContext Current
    {
        get
        {
            var mode = authOptions.Value.Mode;
            return mode switch
            {
                AgentorAuthMode.Fake => ResolveFakeActor(),
                AgentorAuthMode.Header => ResolveHeaderActor(),
                AgentorAuthMode.Jwt => ResolveJwtActor(),
                _ => throw new InvalidOperationException($"Unsupported Agentor auth mode '{mode}'.")
            };
        }
    }

    private static ActorContext ResolveFakeActor()
        => new(FallbackActorId, "local-dev-actor", ActorRole.HumanOperator);

    private ActorContext ResolveHeaderActor()
    {
        var headerName = authOptions.Value.HeaderActorIdHeaderName;
        var ctx = http.HttpContext;
        if (ctx?.Request.Headers.TryGetValue(headerName, out var values) == true
            && Guid.TryParse(values.ToString(), out var id)
            && id != Guid.Empty)
        {
            return new ActorContext(id, $"header:{headerName}", ActorRole.HumanOperator);
        }

        throw new InvalidOperationException(
            $"Header auth mode requires a valid GUID in '{headerName}'.");
    }

    private ActorContext ResolveJwtActor()
    {
        var user = http.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException("JWT auth mode requires an authenticated principal.");
        }

        var actorIdText = authOptions.Value.JwtActorIdClaimTypes
            .Select(user.FindFirstValue)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        if (!Guid.TryParse(actorIdText, out var actorId) || actorId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "JWT auth mode requires a GUID actor id claim (nameidentifier, sub, or oid).");
        }

        var displayName = authOptions.Value.JwtDisplayNameClaimTypes
                              .Select(user.FindFirstValue)
                              .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))
            ?? user.Identity?.Name
            ?? "jwt-principal";

        var roleText = user.FindFirstValue(authOptions.Value.JwtRoleClaimType);
        var role = Enum.TryParse<ActorRole>(roleText, ignoreCase: true, out var parsed)
            ? parsed
            : ActorRole.HumanOperator;

        return new ActorContext(actorId, $"jwt:{displayName}", role);
    }
}
