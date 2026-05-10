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
}
