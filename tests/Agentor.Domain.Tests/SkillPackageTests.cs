using Agentor.Domain.Enums;

namespace Agentor.Domain.Tests;

public sealed class SkillPackageTests
{
    private static readonly AgentRecipeVersion V1 = AgentRecipeVersion.Parse("1.0.0");

    [Fact]
    public void TryCreate_ValidSkill_Succeeds()
    {
        var steps = new[]
        {
            new SkillProcedureStepDefinition("a", 1, "Warm-up", SkillProcedureStepKind.Segment),
            new SkillProcedureStepDefinition("b", 2, "Echo", SkillProcedureStepKind.ToolRef, "fake.echo")
        };

        var ok = SkillPackage.TryCreate(
            Guid.NewGuid(),
            "demo.skill",
            V1,
            "Demo",
            "Demonstrates segment + tool ref.",
            steps,
            out var pkg,
            out var validation);

        Assert.True(ok);
        Assert.NotNull(pkg);
        Assert.True(validation.IsValid);
        Assert.Equal(new[] { "fake.echo" }, pkg!.DeclaredToolKeys);
    }

    [Fact]
    public void TryCreate_DuplicateStepIndex_Fails()
    {
        var steps = new[]
        {
            new SkillProcedureStepDefinition("a", 1, "One", SkillProcedureStepKind.Segment),
            new SkillProcedureStepDefinition("b", 1, "Two", SkillProcedureStepKind.Segment)
        };

        var ok = SkillPackage.TryCreate(Guid.NewGuid(), "k", V1, "n", "p", steps, out _, out var validation);

        Assert.False(ok);
        Assert.False(validation.IsValid);
    }

    [Fact]
    public void TryCreate_ToolRefWithoutKey_Fails()
    {
        var steps = new[] { new SkillProcedureStepDefinition("a", 1, "Bad", SkillProcedureStepKind.ToolRef, "   ") };

        var ok = SkillPackage.TryCreate(Guid.NewGuid(), "k", V1, "n", "p", steps, out _, out var validation);

        Assert.False(ok);
        Assert.Contains(validation.Issues, i => i.Code == "SKILL_TOOLREF_KEY_REQUIRED");
    }

    [Fact]
    public void TryCreate_SegmentWithToolKey_Fails()
    {
        var steps = new[] { new SkillProcedureStepDefinition("a", 1, "Bad", SkillProcedureStepKind.Segment, "fake.echo") };

        var ok = SkillPackage.TryCreate(Guid.NewGuid(), "k", V1, "n", "p", steps, out _, out var validation);

        Assert.False(ok);
        Assert.Contains(validation.Issues, i => i.Code == "SKILL_SEGMENT_TOOLKEY");
    }
}
