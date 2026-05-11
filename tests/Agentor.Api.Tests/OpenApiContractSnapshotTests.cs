using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Agentor.Api.Tests;

/// <summary>
/// Phase 36 / PR145 — checked-in OpenAPI snapshot must match the Test-host document (canonical JSON).
/// Refresh: <c>AGENTOR_UPDATE_OPENAPI_SNAPSHOT=1 dotnet test Agentor.sln --filter FullyQualifiedName~OpenApiContractSnapshotTests</c>
/// </summary>
public sealed class OpenApiContractSnapshotTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OpenApiContractSnapshotTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OpenApi_v1_json_matches_checked_in_snapshot()
    {
        using var client = _factory.CreateClient();
        var liveResponse = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);
        var live = await liveResponse.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(live));

        var snapshotPath = ResolveOpenApiSnapshotPath();
        if (string.Equals(Environment.GetEnvironmentVariable("AGENTOR_UPDATE_OPENAPI_SNAPSHOT"), "1", StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
            using var parsed = JsonDocument.Parse(live);
            var pretty = JsonSerializer.Serialize(parsed.RootElement, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(snapshotPath, pretty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return;
        }

        Assert.True(File.Exists(snapshotPath), $"Missing OpenAPI snapshot at {snapshotPath}. Set AGENTOR_UPDATE_OPENAPI_SNAPSHOT=1 and re-run this test to generate it.");
        var expected = await File.ReadAllTextAsync(snapshotPath);
        Assert.Equal(OpenApiJsonCanonicalizer.Canonicalize(expected), OpenApiJsonCanonicalizer.Canonicalize(live));
    }

    private static string ResolveOpenApiSnapshotPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var sln = Path.Combine(dir.FullName, "Agentor.sln");
            if (File.Exists(sln))
            {
                return Path.Combine(dir.FullName, "docs", "api", "openapi-v1.snapshot.json");
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root (Agentor.sln) from test base directory.");
    }
}
