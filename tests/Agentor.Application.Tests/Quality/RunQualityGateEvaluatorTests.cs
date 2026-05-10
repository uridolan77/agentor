using Agentor.Application.Quality;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Xunit;

namespace Agentor.Application.Tests.Quality;

public sealed class RunQualityGateEvaluatorTests
{
    [Fact]
    public void Evaluate_CompletedRunWithRunCompletedTrace_Passes()
    {
        var run = AgentRun.Start(Guid.NewGuid(), "a", "o", "t", DateTimeOffset.UtcNow);
        run.StartStep("only", DateTimeOffset.UtcNow).Complete(DateTimeOffset.UtcNow);
        run.Complete(DateTimeOffset.UtcNow);
        var summary = RunQualityGateEvaluator.Evaluate(run);
        Assert.True(summary.Passed);
        Assert.Empty(summary.Violations);
    }

    [Fact]
    public void Evaluate_FailedRun_FailsWhenCompletionRequired()
    {
        var run = AgentRun.Start(Guid.NewGuid(), "a", "o", "t", DateTimeOffset.UtcNow);
        run.Fail("x", DateTimeOffset.UtcNow);
        var summary = RunQualityGateEvaluator.Evaluate(run);
        Assert.False(summary.Passed);
        Assert.Contains("RUN_NOT_COMPLETED", summary.Violations);
    }
}