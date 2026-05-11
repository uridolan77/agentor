using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Agentor.Api.Security;

public static class AgentorAuthSchemes
{
    public const string Fake = "Agentor.Fake";

    public const string Header = "Agentor.Header";

    /// <summary>
    /// Parses bearer JWTs without signature validation when
    /// <see cref="AgentorAuthOptions.JwtAcceptUnvalidatedBearerTokens"/> is enabled (gateway/dev only).
    /// </summary>
    public const string JwtUnvalidated = "Agentor.JwtUnvalidated";

    public static string ResolveDefaultAuthenticateScheme(AgentorAuthOptions options)
    {
        return options.Mode switch
        {
            AgentorAuthMode.Fake => Fake,
            AgentorAuthMode.Header => Header,
            AgentorAuthMode.Jwt when !string.IsNullOrWhiteSpace(options.JwtAuthority) =>
                JwtBearerDefaults.AuthenticationScheme,
            AgentorAuthMode.Jwt when options.JwtAcceptUnvalidatedBearerTokens => JwtUnvalidated,
            _ => Fake
        };
    }
}
