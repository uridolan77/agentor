using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agentor.Api.Security;

public static class AgentorAuthenticationExtensions
{
    public static IServiceCollection AddAgentorWebAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authOptions = configuration.GetSection(AgentorAuthOptions.SectionName).Get<AgentorAuthOptions>()
            ?? new AgentorAuthOptions();
        var defaultScheme = AgentorAuthSchemes.ResolveDefaultAuthenticateScheme(authOptions);

        var authBuilder = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = defaultScheme;
            options.DefaultChallengeScheme = defaultScheme;
        });

        authBuilder.AddScheme<AuthenticationSchemeOptions, AgentorFakeAuthenticationHandler>(
            AgentorAuthSchemes.Fake,
            _ => { });
        authBuilder.AddScheme<AuthenticationSchemeOptions, AgentorHeaderAuthenticationHandler>(
            AgentorAuthSchemes.Header,
            _ => { });
        authBuilder.AddScheme<AuthenticationSchemeOptions, AgentorUnvalidatedJwtBearerAuthenticationHandler>(
            AgentorAuthSchemes.JwtUnvalidated,
            _ => { });

        if (!string.IsNullOrWhiteSpace(authOptions.JwtAuthority))
        {
            authBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, jwt =>
            {
                jwt.Authority = authOptions.JwtAuthority;
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters.RoleClaimType = authOptions.JwtRoleClaimType;
                if (!string.IsNullOrWhiteSpace(authOptions.JwtAudience))
                {
                    jwt.Audience = authOptions.JwtAudience;
                }
            });
        }

        return services;
    }

    public static IServiceCollection AddAgentorWebAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AgentorAuthorizationPolicies.Authenticated, policy =>
                policy.RequireAuthenticatedUser());
        });

        return services;
    }
}
