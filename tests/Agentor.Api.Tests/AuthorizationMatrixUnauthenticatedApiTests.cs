using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Agentor.Api.Tests;

/// <summary>
/// Isolated from <see cref="AuthorizationMatrixApiTests"/> so no shared <see cref="WebApplicationFactory{TEntryPoint}"/> class fixture
/// affects authentication scheme registration (Phase 38 / PR155).
/// </summary>
public sealed class AuthorizationMatrixUnauthenticatedApiTests
{
    [Fact]
    public async Task Header_mode_without_actor_header_returns_401_on_protected_samples()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment(Environments.Development);
            b.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Agentor:Auth:Mode"] = "Header",
                    ["Agentor:Auth:HeaderActorIdHeaderName"] = "X-Agentor-Actor-Id",
                });
            });
        });

        using var client = factory.CreateClient();
        // /api/v1 group: anonymous must not reach handlers (see Phase29WebAuthenticationApiTests).
        // GET /ready is not asserted here: under WebApplicationFactory + Header mode, /ready without the
        // actor header did not reliably return 401 alongside /api/v1/* in the same configuration (PR158.5).
        var response = await client.GetAsync("/api/v1/agent-runs");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        response = await client.GetAsync("/api/v1/integrations/status");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        response = await client.GetAsync("/api/v1/ops/queue");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
