using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Domain.Tests;

public sealed class AgentRecipePlanDomainTests
{
    private const string FakeToolKey = "pr1.fake-tool";

    private static readonly AgentRecipeVersion V1 = AgentRecipeVersion.Parse("1.0.0");

    [Fact]
    public void TryCreate_ValidRecipe_Succeeds()
    {
        var ok = AgentRecipe.TryCreate(
            Guid.NewGuid(),
            "demo",
            V1,
            CoordinationTopology.SequentialPipeline,
            [new RecipeStepDefinition("a", 1, RecipeStepKind.Tool, FakeToolKey)],
            null,
            out var recipe,
            out var validation);

        Assert.True(ok);
        Assert.NotNull(recipe);
        Assert.True(validation.IsValid);
    }

    [Fact]
    public void TryCreate_EmptyName_IsInvalid()
    {
        var ok = AgentRecipe.TryCreate(
            Guid.NewGuid(),
            "   ",
            V1,
            CoordinationTopology.SequentialPipeline,
            [new RecipeStepDefinition("a", 1, RecipeStepKind.Tool, FakeToolKey)],
            null,
            out _,
            out var validation);

        Assert.False(ok);
        Assert.Contains(validation.Issues, i => i.Code == "RECIPE_NAME_REQUIRED");
    }

    [Fact]
    public void TryCreate_RecipeWithoutSteps_IsInvalid()
    {
        var ok = AgentRecipe.TryCreate(
            Guid.NewGuid(),
            "empty",
            V1,
            CoordinationTopology.SequentialPipeline,
            [],
            null,
            out _,
            out var validation);

        Assert.False(ok);
        Assert.Contains(validation.Issues, i => i.Code == "RECIPE_NO_STEPS");
    }

    [Fact]
    public void TryCreate_DuplicateStepIndexes_IsInvalid()
    {
        var steps = new[]
        {
            new RecipeStepDefinition("a", 1, RecipeStepKind.Tool, FakeToolKey),
            new RecipeStepDefinition("b", 1, RecipeStepKind.Tool, FakeToolKey)
        };

        var ok = AgentRecipe.TryCreate(Guid.NewGuid(), "dup", V1, CoordinationTopology.SequentialPipeline, steps, null, out _, out var validation);

        Assert.False(ok);
        Assert.Contains(validation.Issues, i => i.Code == "DUPLICATE_STEP_INDEX");
    }

    [Fact]
    public void TryCreate_DuplicateStepIds_IsInvalid()
    {
        var steps = new[]
        {
            new RecipeStepDefinition("same", 1, RecipeStepKind.Tool, FakeToolKey),
            new RecipeStepDefinition("SAME", 2, RecipeStepKind.Tool, FakeToolKey)
        };

        var ok = AgentRecipe.TryCreate(Guid.NewGuid(), "dup-id", V1, CoordinationTopology.SequentialPipeline, steps, null, out _, out var validation);

        Assert.False(ok);
        Assert.Contains(validation.Issues, i => i.Code == "DUPLICATE_STEP_ID");
    }

    [Fact]
    public void TryCreate_InvalidToolKey_IsInvalid()
    {
        var steps = new[] { new RecipeStepDefinition("a", 1, RecipeStepKind.Tool, "   ") };

        var ok = AgentRecipe.TryCreate(Guid.NewGuid(), "bad-tool", V1, CoordinationTopology.SequentialPipeline, steps, null, out _, out var validation);

        Assert.False(ok);
        Assert.Contains(validation.Issues, i => i.Code == "TOOL_KEY_INVALID");
    }

    [Fact]
    public void TryCreate_InvalidGuardOnFirstStep_IsRejected()
    {
        var ok = AgentRecipe.TryCreate(
            Guid.NewGuid(),
            "bad-guard",
            AgentRecipeVersion.Parse("1"),
            CoordinationTopology.SequentialPipeline,
            [
                new RecipeStepDefinition(
                    "a",
                    1,
                    RecipeStepKind.Tool,
                    FakeToolKey,
                    Guard: new StepGuardDefinition(StepGuardKind.PreviousStepSucceeded))
            ],
            null,
            out _,
            out var validation);

        Assert.False(ok);
        Assert.Contains(validation.Issues, i => i.Code == "GUARD_NO_PREVIOUS_STEP");
    }

    [Fact]
    public void Instantiate_PlanStartsReady_AndOrdersStepsDeterministically()
    {
        var ok = AgentRecipe.TryCreate(
            Guid.NewGuid(),
            "order",
            V1,
            CoordinationTopology.SequentialPipeline,
            [
                new RecipeStepDefinition("second", 2, RecipeStepKind.Tool, FakeToolKey),
                new RecipeStepDefinition("first", 1, RecipeStepKind.Tool, FakeToolKey)
            ],
            null,
            out var recipe,
            out _);

        Assert.True(ok);
        Assert.NotNull(recipe);

        var plan = AgentPlan.Instantiate(recipe, Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal(AgentPlanStatus.Ready, plan.Status);
        Assert.Equal(2, plan.Steps.Count);
        Assert.All(plan.Steps, s => Assert.Equal(AgentPlanStepStatus.Pending, s.Status));
        Assert.Equal("first", plan.Steps[0].SourceStepId);
        Assert.Equal("second", plan.Steps[1].SourceStepId);
    }
}
