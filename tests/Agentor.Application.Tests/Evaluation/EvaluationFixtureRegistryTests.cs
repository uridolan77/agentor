using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Application.Coordination;
using Agentor.Application.Evaluation;
using Agentor.Application.Quality;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Infrastructure;
using Agentor.Infrastructure.Conexus;
using Agentor.Infrastructure.ExternalAgents;
using Agentor.Infrastructure.Mcp;
using Microsoft.Extensions.Options;
using Xunit;

namespace Agentor.Application.Tests.Evaluation;

public sealed class EvaluationFixtureRegistryTests
{
    private static (string Dir, string RegistryPath) Paths()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "fixtures", "eval");
        return (dir, Path.Combine(dir, "registry.json"));
    }

    [Fact]
    public void Load_registry_has_expected_entries()
    {
        var (dir, regPath) = Paths();
        var reg = EvaluationFixtureRegistry.Load(regPath, dir);
        Assert.Equal(5, reg.Entries.Count); // Phase 18 + Phase 34 skill-resume-audit-export
        Assert.Contains(reg.Entries, e => e.Id == "one-step-fake-tool");
        Assert.Contains(reg.Entries, e => e.Id == "external-agent-one-call");
        Assert.Contains(reg.Entries, e => e.Id == "review-gated-multistep-plan");
        Assert.Contains(reg.Entries, e => e.Id == "review-resume-audit-export");
        Assert.Contains(reg.Entries, e => e.Id == "skill-resume-audit-export");
    }

    [Fact]
    public void DiscoverFixtureFiles_excludes_registry()
    {
        var (dir, _) = Paths();
        var files = EvaluationFixtureRegistry.DiscoverFixtureFiles(dir);
        Assert.DoesNotContain(files, f => string.Equals(Path.GetFileName(f), "registry.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, f => f.EndsWith("evaluation-harness-one-step-tool.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LoadHarnessFixture_parses_expected_snapshot()
    {
        var (dir, regPath) = Paths();
        var reg = EvaluationFixtureRegistry.Load(regPath, dir);
        var def = reg.LoadHarnessFixture("one-step-fake-tool");
        Assert.Equal(2, def.SchemaVersion);
        Assert.NotNull(def.ExpectedSnapshot);
        Assert.Equal("Completed", def.ExpectedSnapshot!.RunStatus);
    }
}
