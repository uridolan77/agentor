using Agentor.Contracts.KnowledgeState;
using Agentor.Infrastructure;
using Agentor.Infrastructure.Athanor;
using Agentor.Infrastructure.Options;
using Agentor.Infrastructure.Smoke;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Agentor.IntegrationSmoke;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        IntegrationSmokeParsedArgs parsed;
        try
        {
            parsed = IntegrationSmokeCommandLine.Parse(args);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }

        var builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                Args = Array.Empty<string>(),
                ContentRootPath = AppContext.BaseDirectory,
            });

        var smokePreview = builder.Configuration.GetSection(IntegrationSmokeOptions.SectionName).Get<IntegrationSmokeOptions>()
            ?? new IntegrationSmokeOptions();
        foreach (var kv in IntegrationSmokeConfigurationMerger.BuildIntegrationModePatches(smokePreview))
        {
            builder.Configuration[kv.Key] = kv.Value;
        }

        builder.Services.Configure<IntegrationSmokeOptions>(builder.Configuration.GetSection(IntegrationSmokeOptions.SectionName));
        builder.Services.AddAgentorInfrastructure(builder.Configuration);
        builder.Services.AddSingleton<IntegrationSmokeRunner>();

        using var host = builder.Build();

        ValidateHttpBaseUrls(host.Services.GetRequiredService<IOptions<AgentorIntegrationsOptions>>().Value);

        var smoke = host.Services.GetRequiredService<IOptions<IntegrationSmokeOptions>>().Value;
        if (smoke.Athanor.Mode == SmokeMode.Fake)
        {
            SeedFakeAthanor(host.Services.GetRequiredService<FakeKnowledgeStateClient>(), smoke);
        }

        var runner = host.Services.GetRequiredService<IntegrationSmokeRunner>();
        var report = await runner.RunAsync(parsed.OnlyTargets, CancellationToken.None).ConfigureAwait(false);
        await IntegrationSmokeReportWriter.WriteAsync(parsed.OutputDirectory, report, CancellationToken.None).ConfigureAwait(false);

        Console.WriteLine(report.OverallOk ? "Integration smoke: OK" : "Integration smoke: FAILED");
        return report.OverallOk ? 0 : 1;
    }

    private static void ValidateHttpBaseUrls(AgentorIntegrationsOptions integrations)
    {
        RequireBaseUrl(integrations.Athanor, "Agentor:Integrations:Athanor");
        RequireBaseUrl(integrations.Conexus, "Agentor:Integrations:Conexus");
        RequireBaseUrl(integrations.Mcp, "Agentor:Integrations:Mcp");
        RequireBaseUrl(integrations.ExternalAgents, "Agentor:Integrations:ExternalAgents");
    }

    private static void RequireBaseUrl(IntegrationFamilyOptions family, string label)
    {
        if (family.Mode != IntegrationAdapterMode.Http)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(family.Http?.BaseUrl))
        {
            throw new InvalidOperationException(
                $"{label}:Http:BaseUrl is required when that integration is in Http mode (set via {IntegrationSmokeOptions.SectionName} smoke modes).");
        }
    }

    private static void SeedFakeAthanor(FakeKnowledgeStateClient fake, IntegrationSmokeOptions smoke)
    {
        var pid = smoke.AthanorProjectId;
        var snapshot = new CanonicalSnapshotDto(
            Guid.NewGuid(),
            pid,
            DateTimeOffset.UtcNow,
            [new CanonicalStateEntryDto(smoke.AthanorCanonicalLookupKey, "integration-smoke-value", 1.0)]);
        fake.SeedLatestSnapshot(snapshot);
        fake.SeedSearchResults(pid, smoke.AthanorEvidenceSearchQuery, [new EvidenceSearchResultDto(Guid.NewGuid(), "smoke-title", "smoke-snippet")]);
    }

}
