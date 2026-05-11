using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agentor.Api.Security;

/// <summary>
/// Authenticates bearer tokens as JWTs without validating signatures or issuer.
/// Only enabled when <see cref="AgentorAuthOptions.JwtAcceptUnvalidatedBearerTokens"/> is true.
/// </summary>
public sealed class AgentorUnvalidatedJwtBearerAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization header."));
        }

        var raw = authHeader.ToString();
        const string prefix = "Bearer ";
        if (raw.Length < prefix.Length || !raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("Authorization header must be a Bearer token."));
        }

        var token = raw[prefix.Length..].Trim();
        if (string.IsNullOrEmpty(token))
        {
            return Task.FromResult(AuthenticateResult.Fail("Bearer token is empty."));
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
            {
                return Task.FromResult(AuthenticateResult.Fail("Bearer token is not a readable JWT."));
            }

            var jwt = handler.ReadJwtToken(token);
            var identity = new ClaimsIdentity(jwt.Claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AuthenticateResult.Fail($"Invalid JWT: {ex.Message}"));
        }
    }
}
