using Agentor.Application.Evaluation;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Infrastructure;
using Xunit;

namespace Agentor.Application.Tests.Evaluation;

public sealed class QualityRuleSetEvaluatorTests
{
    [Fact]
    public void Parse_rejects_unknown_predicate()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "eval", "quality-rules-invalid-predicate.json");
        var json = File.ReadAllText(path);
        Assert.Throws<InvalidDataException>(() => QualityRuleSetEvaluator.Parse(json));
    }

    [Fact]
    public void Evaluate_valid_rules_passes_on_completed_run()
    {
        var clock = new SystemClock();
        var run = AgentRun.Start(Guid.NewGuid(), "q", "o", "t", clock.UtcNow);
        var step = run.StartStep("s", clock.UtcNow);
        step.Complete(clock.UtcNow);
        for (var i = 0; i < 6; i++)
        {
            run.RecordTrace(TraceEventKind.PolicyEvaluated, "p", clock.UtcNow, null);
        }

        run.Complete(clock.UtcNow);

        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "eval", "quality-rules-valid.json"));
        var set = QualityRuleSetEvaluator.Parse(json);
        var result = QualityRuleSetEvaluator.Evaluate(set, run);
        Assert.True(result.Passed);
        Assert.Empty(result.Violations);
    }
}
