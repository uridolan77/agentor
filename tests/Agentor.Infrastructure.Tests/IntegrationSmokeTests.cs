using Agentor.Infrastructure.Athanor;
using Agentor.Infrastructure.Http;
using Agentor.Infrastructure.Options;
using Agentor.Infrastructure.Smoke;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Agentor.Infrastructure.Tests;

public sealed class IntegrationSmokeTargetValidationTests
{
    [Fact]
    public void Validate_accepts_empty_list()
    {
        IntegrationSmokeTargetValidation.Validate([]);
    }

    [Fact]
    public void Validate_accepts_known_targets_case_insensitive()
    {
        IntegrationSmokeTargetValidation.Validate(["athanor", "Conexus", "MCP", "externalagents"]);
    }

    [Fact]
    public void Validate_throws_on_unknown_target()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            IntegrationSmokeTargetValidation.Validate(["NotARealTarget"]));
        Assert.Contains("NotARealTarget", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Unknown smoke target", ex.Message, StringComparison.Ordinal);
    }
}

public sealed class IntegrationSmokeCommandLineTests
{
    [Fact]
    public void Parse_no_args_uses_default_output_and_no_target_filter()
    {
        var parsed = IntegrationSmokeCommandLine.Parse([], currentDirectory: "C:/work");
        Assert.Null(parsed.OnlyTargets);
        Assert.Equal(Path.Combine("C:/work", "artifacts", "integration-smoke"), parsed.OutputDirectory);
    }

    [Fact]
    public void Parse_accepts_target_and_output_flags()
    {
        var parsed = IntegrationSmokeCommandLine.Parse(
            ["--target", "Athanor", "-o", "C:/out", "-t", "Conexus"],
            currentDirectory: "ignored");

        Assert.Equal("C:/out", parsed.OutputDirectory);
        Assert.NotNull(parsed.OnlyTargets);
        Assert.Contains("Athanor", parsed.OnlyTargets!);
        Assert.Contains("Conexus", parsed.OnlyTargets!);
    }

    [Theory]
    [InlineData("--target")]
    [InlineData("-t")]
    public void Parse_target_without_value_throws(string flag)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            IntegrationSmokeCommandLine.Parse([flag], currentDirectory: "C:/work"));
        Assert.Contains(flag, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--output")]
    [InlineData("-o")]
    public void Parse_output_without_value_throws(string flag)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            IntegrationSmokeCommandLine.Parse([flag], currentDirectory: "C:/work"));
        Assert.Contains(flag, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_target_followed_by_flag_throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            IntegrationSmokeCommandLine.Parse(["--target", "--output", "C:/out"], currentDirectory: "C:/work"));
        Assert.Contains("--target", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_unknown_flag_throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            IntegrationSmokeCommandLine.Parse(["--mystery"], currentDirectory: "C:/work"));
        Assert.Contains("--mystery", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Unknown CLI argument", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_unknown_target_value_throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            IntegrationSmokeCommandLine.Parse(["--target", "NotARealTarget"], currentDirectory: "C:/work"));
        Assert.Contains("NotARealTarget", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Unknown smoke target", ex.Message, StringComparison.Ordinal);
    }
}

public sealed class IntegrationSmokeConfigurationMergerTests
{
    [Fact]
    public void BuildIntegrationModePatches_maps_each_family()
    {
        var smoke = new IntegrationSmokeOptions
        {
            Athanor = new IntegrationSmokeFamilyOptions { Mode = SmokeMode.Http },
            Conexus = new IntegrationSmokeFamilyOptions { Mode = SmokeMode.Fake },
            Mcp = new IntegrationSmokeFamilyOptions { Mode = SmokeMode.Disabled },
            ExternalAgents = new IntegrationSmokeFamilyOptions { Mode = SmokeMode.Http },
        };

        var p = IntegrationSmokeConfigurationMerger.BuildIntegrationModePatches(smoke);
        Assert.Equal(nameof(IntegrationAdapterMode.Http), p["Agentor:Integrations:Athanor:Mode"]);
        Assert.Equal(nameof(IntegrationAdapterMode.Fake), p["Agentor:Integrations:Conexus:Mode"]);
        Assert.Equal(nameof(IntegrationAdapterMode.Disabled), p["Agentor:Integrations:Mcp:Mode"]);
        Assert.Equal(nameof(IntegrationAdapterMode.Http), p["Agentor:Integrations:ExternalAgents:Mode"]);
    }

    [Theory]
    [InlineData(SmokeMode.Disabled, IntegrationAdapterMode.Disabled)]
    [InlineData(SmokeMode.Fake, IntegrationAdapterMode.Fake)]
    [InlineData(SmokeMode.Http, IntegrationAdapterMode.Http)]
    public void Map_translates_smoke_mode(SmokeMode smoke, IntegrationAdapterMode expected) =>
        Assert.Equal(expected, IntegrationSmokeConfigurationMerger.Map(smoke));
}

public sealed class IntegrationSmokeFakeRunnerTests
{
    [Fact]
    public async Task Runner_all_fake_modes_produces_ok_report()
    {
        var keys = new Dictionary<string, string?>
        {
            ["Agentor:IntegrationSmoke:Athanor:Mode"] = nameof(SmokeMode.Fake),
            ["Agentor:IntegrationSmoke:Conexus:Mode"] = nameof(SmokeMode.Fake),
            ["Agentor:IntegrationSmoke:Mcp:Mode"] = nameof(SmokeMode.Fake),
            ["Agentor:IntegrationSmoke:ExternalAgents:Mode"] = nameof(SmokeMode.Fake),
        };

        var boot = new ConfigurationBuilder().AddInMemoryCollection(keys).Build();
        var smokePreview = boot.GetSection(IntegrationSmokeOptions.SectionName).Get<IntegrationSmokeOptions>()!;
        foreach (var kv in IntegrationSmokeConfigurationMerger.BuildIntegrationModePatches(smokePreview))
        {
            keys[kv.Key] = kv.Value;
        }

        var hb = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        hb.Configuration.AddInMemoryCollection(keys);
        hb.Services.Configure<IntegrationSmokeOptions>(hb.Configuration.GetSection(IntegrationSmokeOptions.SectionName));
        hb.Services.AddAgentorInfrastructure(hb.Configuration);
        hb.Services.AddSingleton<IntegrationSmokeRunner>();

        using var host = hb.Build();

        var smoke = host.Services.GetRequiredService<IOptions<IntegrationSmokeOptions>>().Value;
        SeedFakeAthanor(host.Services.GetRequiredService<FakeKnowledgeStateClient>(), smoke);

        var runner = host.Services.GetRequiredService<IntegrationSmokeRunner>();
        var report = await runner.RunAsync(null, CancellationToken.None);

        Assert.True(report.OverallOk, string.Join("; ", report.Steps.Where(s => !s.Ok).Select(s => $"{s.Target}.{s.Name}")));
        Assert.NotEmpty(report.Steps);
    }

    [Fact]
    public async Task Runner_explicit_target_with_all_smoke_disabled_fails_with_cli_diagnostic()
    {
        var keys = new Dictionary<string, string?>
        {
            ["Agentor:IntegrationSmoke:Athanor:Mode"] = nameof(SmokeMode.Disabled),
            ["Agentor:IntegrationSmoke:Conexus:Mode"] = nameof(SmokeMode.Disabled),
            ["Agentor:IntegrationSmoke:Mcp:Mode"] = nameof(SmokeMode.Disabled),
            ["Agentor:IntegrationSmoke:ExternalAgents:Mode"] = nameof(SmokeMode.Disabled),
        };

        var boot = new ConfigurationBuilder().AddInMemoryCollection(keys).Build();
        var smokePreview = boot.GetSection(IntegrationSmokeOptions.SectionName).Get<IntegrationSmokeOptions>()!;
        foreach (var kv in IntegrationSmokeConfigurationMerger.BuildIntegrationModePatches(smokePreview))
        {
            keys[kv.Key] = kv.Value;
        }

        var hb = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        hb.Configuration.AddInMemoryCollection(keys);
        hb.Services.Configure<IntegrationSmokeOptions>(hb.Configuration.GetSection(IntegrationSmokeOptions.SectionName));
        hb.Services.AddAgentorInfrastructure(hb.Configuration);
        hb.Services.AddSingleton<IntegrationSmokeRunner>();

        using var host = hb.Build();
        var runner = host.Services.GetRequiredService<IntegrationSmokeRunner>();
        var report = await runner.RunAsync(new HashSet<string>(["Athanor"], StringComparer.OrdinalIgnoreCase), CancellationToken.None);

        Assert.False(report.OverallOk);
        var cli = Assert.Single(report.Steps);
        Assert.Equal("Cli", cli.Target);
        Assert.Equal("explicitTargetNoWork", cli.Name);
        Assert.False(cli.Ok);
    }

    private static void SeedFakeAthanor(FakeKnowledgeStateClient fake, IntegrationSmokeOptions smoke)
    {
        var pid = smoke.AthanorProjectId;
        var snapshot = new Agentor.Contracts.KnowledgeState.CanonicalSnapshotDto(
            Guid.NewGuid(),
            pid,
            DateTimeOffset.UtcNow,
            [new Agentor.Contracts.KnowledgeState.CanonicalStateEntryDto(smoke.AthanorCanonicalLookupKey, "v", 1.0)]);
        fake.SeedLatestSnapshot(snapshot);
        fake.SeedSearchResults(
            pid,
            smoke.AthanorEvidenceSearchQuery,
            [new Agentor.Contracts.KnowledgeState.EvidenceSearchResultDto(Guid.NewGuid(), "t", "s")]);
    }
}

public sealed class IntegrationFailureRedactionTests
{
    [Fact]
    public void RedactAndTruncate_strips_bearer()
    {
        var s = IntegrationFailureRedaction.RedactAndTruncate("Authorization: Bearer supersecret");
        Assert.Contains("[REDACTED]", s, StringComparison.Ordinal);
        Assert.DoesNotContain("supersecret", s, StringComparison.Ordinal);
    }
}

public sealed class IntegrationSmokeReportWriterTests
{
    [Fact]
    public async Task WriteAsync_creates_json_and_markdown()
    {
        var dir = Path.Combine(Path.GetTempPath(), "agentor-int-smoke-" + Guid.NewGuid().ToString("N"));
        try
        {
            var report = new IntegrationSmokeReport
            {
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                OverallOk = true,
                Steps =
                [
                    new SmokeStepRecord { Target = "Athanor", Name = "latestSnapshot", Ok = true, Detail = "ok" },
                ],
            };

            await IntegrationSmokeReportWriter.WriteAsync(dir, report);

            Assert.True(File.Exists(Path.Combine(dir, "integration-smoke-report.json")));
            Assert.True(File.Exists(Path.Combine(dir, "integration-smoke-report.md")));
        }
        finally
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch
            {
                // best-effort cleanup on temp
            }
        }
    }

    [Fact]
    public async Task WriteAsync_redacts_raw_bearer_in_detail_on_disk()
    {
        var dir = Path.Combine(Path.GetTempPath(), "agentor-int-smoke-redact-" + Guid.NewGuid().ToString("N"));
        try
        {
            var report = new IntegrationSmokeReport
            {
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                OverallOk = false,
                Steps =
                [
                    new SmokeStepRecord
                    {
                        Target = "Test",
                        Name = "rawDetail",
                        Ok = false,
                        Detail = "Authorization: Bearer ultra-secret-token-999",
                    },
                ],
            };

            await IntegrationSmokeReportWriter.WriteAsync(dir, report);

            var json = await File.ReadAllTextAsync(Path.Combine(dir, "integration-smoke-report.json"));
            var md = await File.ReadAllTextAsync(Path.Combine(dir, "integration-smoke-report.md"));
            Assert.DoesNotContain("ultra-secret-token-999", json, StringComparison.Ordinal);
            Assert.DoesNotContain("ultra-secret-token-999", md, StringComparison.Ordinal);
            Assert.Contains("REDACTED", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("REDACTED", md, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch
            {
                // best-effort cleanup on temp
            }
        }
    }
}
