using Agentor.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Agentor.Api.Security;

public sealed class HeaderOrFakeActorAccessor(IHttpContextAccessor http) : ICurrentActorAccessor
{
    private static readonly Guid FallbackActorId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    public ActorContext Current
    {
        get
        {
            var ctx = http.HttpContext;
            if (ctx?.Request.Headers.TryGetValue("X-Agentor-Actor-Id", out var values) == true
                && Guid.TryParse(values.ToString(), out var id)
                && id != Guid.Empty)
            {
                return new ActorContext(id, "header:X-Agentor-Actor-Id", ActorRole.HumanOperator);
            }

            return new ActorContext(FallbackActorId, "local-dev-actor", ActorRole.HumanOperator);
        }
    }
}
