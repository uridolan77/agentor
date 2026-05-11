using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Agentor.Api.Security;

public enum AgentorAuthMode
{
    Fake,
    Header,
    Jwt
}

public sealed class AgentorAuthOptions
{
    public const string SectionName = "Agentor:Auth";

    public AgentorAuthMode Mode { get; set; } = AgentorAuthMode.Fake;

    public bool AllowFakeOutsideDevelopment { get; set; }

    [Required]
    public string HeaderActorIdHeaderName { get; set; } = "X-Agentor-Actor-Id";

    public List<string> JwtActorIdClaimTypes { get; set; } =
    [
        ClaimTypes.NameIdentifier,
        "sub",
        "oid"
    ];

    public List<string> JwtDisplayNameClaimTypes { get; set; } =
    [
        ClaimTypes.Name,
        "name",
        "preferred_username"
    ];

    [Required]
    public string JwtRoleClaimType { get; set; } = ClaimTypes.Role;

    /// <summary>
    /// When set (and <see cref="Mode"/> is <see cref="AgentorAuthMode.Jwt"/>), registers in-process
    /// JWT bearer validation (<c>AddJwtBearer</c>) against this OIDC authority.
    /// </summary>
    public string? JwtAuthority { get; set; }

    /// <summary>
    /// Optional audience for JWT bearer validation when <see cref="JwtAuthority"/> is set.
    /// </summary>
    public string? JwtAudience { get; set; }

    /// <summary>
    /// When <see cref="Mode"/> is <see cref="AgentorAuthMode.Jwt"/> and <see cref="JwtAuthority"/> is not set,
    /// enables parsing bearer tokens without signature validation so gateways can forward raw JWTs
    /// while Agentor still builds <see cref="Microsoft.AspNetCore.Http.HttpContext.User"/> for authorization.
    /// Unsafe unless the network path is strictly trusted.
    /// </summary>
    public bool JwtAcceptUnvalidatedBearerTokens { get; set; }
}
