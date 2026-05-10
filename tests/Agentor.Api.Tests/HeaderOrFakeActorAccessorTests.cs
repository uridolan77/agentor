using Agentor.Api.Security;
using Agentor.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Xunit;

namespace Agentor.Api.Tests;

public sealed class HeaderOrFakeActorAccessorTests
{
    private static readonly Guid Fallback = Guid.Parse("11111111-1111-4111-8111-111111111111");

    [Fact]
    public void Current_UsesXAgentorActorIdHeader_WhenValidGuid()
    {
        var expected = Guid.Parse("33333333-3333-4333-8333-333333333333");
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Agentor-Actor-Id"] = expected.ToString("D");
        var accessor = new HttpContextAccessor { HttpContext = http };
        ICurrentActorAccessor sut = CreateSut(accessor, AgentorAuthMode.Header);

        Assert.Equal(expected, sut.Current.ActorId);
        Assert.Equal("header:X-Agentor-Actor-Id", sut.Current.DisplayName);
        Assert.Equal(ActorRole.HumanOperator, sut.Current.Role);
    }

    [Fact]
    public void Current_UsesLocalDevFallback_WhenHeaderMissing()
    {
        var http = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = http };
        ICurrentActorAccessor sut = CreateSut(accessor, AgentorAuthMode.Fake);

        Assert.Equal(Fallback, sut.Current.ActorId);
        Assert.Equal("local-dev-actor", sut.Current.DisplayName);
    }

    [Fact]
    public void Current_HeaderMode_Throws_WhenHeaderMissing()
    {
        var http = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = http };
        ICurrentActorAccessor sut = CreateSut(accessor, AgentorAuthMode.Header);

        var ex = Assert.Throws<InvalidOperationException>(() => _ = sut.Current);
        Assert.Contains("X-Agentor-Actor-Id", ex.Message);
    }

    [Fact]
    public void Current_JwtMode_UsesAuthenticatedPrincipalClaim()
    {
        var expected = Guid.Parse("44444444-4444-4444-8444-444444444444");
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, expected.ToString("D")),
                new Claim(ClaimTypes.Name, "reviewer"),
                new Claim(ClaimTypes.Role, "HumanOperator")
            ],
            authenticationType: "test-jwt"))
        };

        var accessor = new HttpContextAccessor { HttpContext = http };
        ICurrentActorAccessor sut = CreateSut(accessor, AgentorAuthMode.Jwt);

        Assert.Equal(expected, sut.Current.ActorId);
        Assert.Equal("jwt:reviewer", sut.Current.DisplayName);
    }

    [Fact]
    public void Current_JwtMode_Throws_WhenRoleClaimMissing()
    {
        var expected = Guid.Parse("66666666-6666-4666-8666-666666666666");
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, expected.ToString("D")),
                new Claim(ClaimTypes.Name, "reviewer")
            ],
            authenticationType: "test-jwt"))
        };

        var accessor = new HttpContextAccessor { HttpContext = http };
        ICurrentActorAccessor sut = CreateSut(accessor, AgentorAuthMode.Jwt);

        var ex = Assert.Throws<InvalidOperationException>(() => _ = sut.Current);
        Assert.Contains("requires a role claim", ex.Message);
    }

    [Fact]
    public void Current_JwtMode_Throws_WhenRoleClaimInvalid()
    {
        var expected = Guid.Parse("77777777-7777-4777-8777-777777777777");
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, expected.ToString("D")),
                new Claim(ClaimTypes.Name, "reviewer"),
                new Claim(ClaimTypes.Role, "RootAdmin")
            ],
            authenticationType: "test-jwt"))
        };

        var accessor = new HttpContextAccessor { HttpContext = http };
        ICurrentActorAccessor sut = CreateSut(accessor, AgentorAuthMode.Jwt);

        var ex = Assert.Throws<InvalidOperationException>(() => _ = sut.Current);
        Assert.Contains("is not recognized", ex.Message);
    }

    [Fact]
    public void Current_JwtMode_UsesConfiguredClaimTypes_AndRole()
    {
        var expected = Guid.Parse("55555555-5555-4555-8555-555555555555");
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("custom_actor", expected.ToString("D")),
                new Claim("custom_name", "ops-user"),
                new Claim("custom_role", "Service")
            ],
            authenticationType: "test-jwt"))
        };

        var accessor = new HttpContextAccessor { HttpContext = http };
        var options = Options.Create(new AgentorAuthOptions
        {
            Mode = AgentorAuthMode.Jwt,
            JwtActorIdClaimTypes = ["custom_actor"],
            JwtDisplayNameClaimTypes = ["custom_name"],
            JwtRoleClaimType = "custom_role"
        });

        ICurrentActorAccessor sut = new HeaderOrFakeActorAccessor(accessor, options);

        Assert.Equal(expected, sut.Current.ActorId);
        Assert.Equal("jwt:ops-user", sut.Current.DisplayName);
        Assert.Equal(ActorRole.Service, sut.Current.Role);
    }

    private static ICurrentActorAccessor CreateSut(
        IHttpContextAccessor accessor,
        AgentorAuthMode mode)
    {
        var options = Options.Create(new AgentorAuthOptions
        {
            Mode = mode,
            HeaderActorIdHeaderName = "X-Agentor-Actor-Id"
        });

        return new HeaderOrFakeActorAccessor(accessor, options);
    }
}
