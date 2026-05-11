using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Agentor.Api.Tests;

public sealed class Phase29WebAuthenticationApiTests
{
    [Fact]
    public async Task HeaderMode_MissingActorHeader_ReturnsUnauthorized_OnApiV1()
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
        var response = await client.GetAsync("/api/v1/agent-runs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HeaderMode_ValidActorHeader_ReturnsOk_OnListRuns()
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
        client.DefaultRequestHeaders.Add("X-Agentor-Actor-Id", "33333333-3333-4333-8333-333333333333");

        var response = await client.GetAsync("/api/v1/agent-runs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
