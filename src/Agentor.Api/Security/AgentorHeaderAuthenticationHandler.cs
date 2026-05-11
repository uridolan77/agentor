using System.Security.Claims;
using System.Text.Encodings.Web;
using Agentor.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agentor.Api.Security;

public sealed class AgentorHeaderAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IOptionsMonitor<AgentorAuthOptions> authOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headerName = authOptions.CurrentValue.HeaderActorIdHeaderName;
        if (!Request.Headers.TryGetValue(headerName, out var values))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Missing '{headerName}'."));
        }

        if (!Guid.TryParse(values.ToString(), out var id) || id == Guid.Empty)
        {
            return Task.FromResult(AuthenticateResult.Fail($"Invalid GUID in '{headerName}'."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString("D")),
            new Claim(ClaimTypes.Role, nameof(ActorRole.HumanOperator)),
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
