using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Agentor.Api.Tests;

public sealed class OpenApiExposureApiTests
{
    [Fact]
    public async Task Development_Environment_ExposesOpenApiDocument()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(w =>
        {
            w.UseEnvironment(Environments.Development);
        });

        using var client = factory.CreateClient();
        var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("openapi", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Production_Default_DoesNotExposeOpenApiDocument()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(w =>
        {
            w.UseEnvironment(Environments.Production);
            w.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Agentor:Auth:AllowFakeOutsideDevelopment"] = "true",
                        ["Agentor:OpenApi:Enabled"] = "false"
                    });
            });
        });

        using var client = factory.CreateClient();
        var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Production_OpenApiEnabled_ExposesOpenApiDocument()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(w =>
        {
            w.UseEnvironment(Environments.Production);
            w.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Agentor:Auth:AllowFakeOutsideDevelopment"] = "true",
                        ["Agentor:OpenApi:Enabled"] = "true"
                    });
            });
        });

        using var client = factory.CreateClient();
        var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
