namespace Agentor.Api.Security;

public static class AgentorAuthorizationPolicies
{
    /// <summary>Requires <see cref="System.Security.Claims.ClaimsPrincipal.Identity"/> authenticated (ASP.NET layer).</summary>
    public const string Authenticated = "Agentor.Authenticated";
}
