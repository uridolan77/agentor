using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Infrastructure;
using Xunit;

namespace Agentor.Application.Tests;

public sealed class ToolExecutionPipelineTests
{
    private static AgentRun CreateRun(IClock clock)
    {
        var profile = AgentProfile.Create("T", "Test profile.", clock.UtcNow);
        return AgentRun.Start(profile.Id, profile.Name, "Objective.", "trace-pipeline", clock.UtcNow);
    }

    [Fact]
    public async Task ExecuteAsync_Succeeds_OnFirstAttempt_WithDurationAndAttemptCount()
    {
        var clock = new SystemClock();
        var run = CreateRun(clock);
        var step = run.StartStep("step", clock.UtcNow);
        var toolCall = ToolCall.Start(run.Id, step.Id, WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>(), clock.UtcNow);
        var executor = new FakeToolExecutor();
        var opts = Microsoft.Extensions.Options.Options.Create(new ToolExecutionOptions { TimeoutMilliseconds = 30_000, MaxAttempts = 3 });
        var pipeline = new ToolExecutionPipeline(clock, opts);

        var request = new ToolExecutionRequest(run.Id, step.Id, WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>
        {
            ["objective"] = "x"
        });

        var result = await pipeline.ExecuteAsync(run, step.Id, toolCall.Id, executor, request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.AttemptsUsed);
        Assert.True(result.TotalDuration >= TimeSpan.Zero);
        Assert.Contains(run.Trace, e => e.Kind == TraceEventKind.ToolExecutionAttemptStarted);
        Assert.Contains(run.Trace, e => e.Kind == TraceEventKind.ToolExecutionAttemptFinished);
        var finished = run.Trace.Last(e => e.Kind == TraceEventKind.ToolExecutionAttemptFinished);
        Assert.Equal("success", finished.Data["outcome"]);
        Assert.True(finished.Data.ContainsKey("durationMs"));
    }

    [Fact]
    public async Task ExecuteAsync_Timeout_ProducesStructuredFailure_AndFinalTimedOutTrace()
    {
        var clock = new SystemClock();
        var run = CreateRun(clock);
        var step = run.StartStep("step", clock.UtcNow);
        var toolCall = ToolCall.Start(run.Id, step.Id, WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>(), clock.UtcNow);
        var slowExecutor = new SlowToolExecutor(TimeSpan.FromSeconds(30));
        var opts = Microsoft.Extensions.Options.Options.Create(new ToolExecutionOptions
        {
            TimeoutMilliseconds = 20,
            MaxAttempts = 1,
            RetryDelayMilliseconds = 0
        });
        var pipeline = new ToolExecutionPipeline(clock, opts);

        var request = new ToolExecutionRequest(run.Id, step.Id, WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>());

        var result = await pipeline.ExecuteAsync(run, step.Id, toolCall.Id, slowExecutor, request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ToolPipelineFailureKind.Timeout, result.FailureKind);
        Assert.Equal("Tool execution timed out.", result.ErrorMessage);
        Assert.Equal(1, result.AttemptsUsed);
        Assert.Contains(run.Trace, e => e.Kind == TraceEventKind.ToolCallTimedOut);
    }

    [Fact]
    public async Task ExecuteAsync_RetriesUntilSuccess_CountsAttemptsAndEmitsRetryTrace()
    {
        var clock = new SystemClock();
        var run = CreateRun(clock);
        var step = run.StartStep("step", clock.UtcNow);
        var toolCall = ToolCall.Start(run.Id, step.Id, WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>(), clock.UtcNow);
        var flaky = new FlakyToolExecutor(failCountBeforeSuccess: 2);
        var opts = Microsoft.Extensions.Options.Options.Create(new ToolExecutionOptions
        {
            TimeoutMilliseconds = 10_000,
            MaxAttempts = 5,
            RetryDelayMilliseconds = 0
        });
        var pipeline = new ToolExecutionPipeline(clock, opts);
        var request = new ToolExecutionRequest(run.Id, step.Id, WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>());

        var result = await pipeline.ExecuteAsync(run, step.Id, toolCall.Id, flaky, request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(3, result.AttemptsUsed);
        Assert.Equal(3, flaky.InvocationCount);
        Assert.Equal(2, run.Trace.Count(e => e.Kind == TraceEventKind.ToolCallRetrying));
    }

    [Fact]
    public async Task ExecuteAsync_CancellationDuringExecution_TracesCanceled_AndDoesNotRetry()
    {
        var clock = new SystemClock();
        var run = CreateRun(clock);
        var step = run.StartStep("step", clock.UtcNow);
        var toolCall = ToolCall.Start(run.Id, step.Id, WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>(), clock.UtcNow);
        var slowExecutor = new SlowToolExecutor(TimeSpan.FromSeconds(30));
        var opts = Microsoft.Extensions.Options.Options.Create(new ToolExecutionOptions
        {
            TimeoutMilliseconds = 60_000,
            MaxAttempts = 5,
            RetryDelayMilliseconds = 0
        });
        var pipeline = new ToolExecutionPipeline(clock, opts);
        var request = new ToolExecutionRequest(run.Id, step.Id, WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>());

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(40));

        var result = await pipeline.ExecuteAsync(run, step.Id, toolCall.Id, slowExecutor, request, cts.Token);

        Assert.False(result.Success);
        Assert.Equal(ToolPipelineFailureKind.Canceled, result.FailureKind);
        Assert.Contains(run.Trace, e => e.Kind == TraceEventKind.ToolCallCanceled);
        Assert.Equal(1, slowExecutor.InvocationCount);
        Assert.Equal(1, result.AttemptsUsed);
        Assert.DoesNotContain(run.Trace, e => e.Kind == TraceEventKind.ToolCallRetrying);
    }

    [Fact]
    public async Task ExecuteAsync_PreCanceled_ReturnsCanceledWithoutExecutorInvocation()
    {
        var clock = new SystemClock();
        var run = CreateRun(clock);
        var step = run.StartStep("step", clock.UtcNow);
        var toolCall = ToolCall.Start(run.Id, step.Id, WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>(), clock.UtcNow);
        var slowExecutor = new SlowToolExecutor(TimeSpan.FromSeconds(30));
        var opts = Microsoft.Extensions.Options.Options.Create(new ToolExecutionOptions());
        var pipeline = new ToolExecutionPipeline(clock, opts);
        var request = new ToolExecutionRequest(run.Id, step.Id, WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>());

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await pipeline.ExecuteAsync(run, step.Id, toolCall.Id, slowExecutor, request, cts.Token);

        Assert.False(result.Success);
        Assert.Equal(ToolPipelineFailureKind.Canceled, result.FailureKind);
        Assert.Equal(0, result.AttemptsUsed);
        Assert.Equal(0, slowExecutor.InvocationCount);
    }

    private sealed class SlowToolExecutor : IToolExecutor
    {
        private readonly TimeSpan _delay;

        public int InvocationCount;

        public SlowToolExecutor(TimeSpan delay) => _delay = delay;

        public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken)
        {
            InvocationCount++;
            await Task.Delay(_delay, cancellationToken);
            return new ToolExecutionResult(true, new Dictionary<string, string>());
        }
    }

    private sealed class FlakyToolExecutor : IToolExecutor
    {
        private readonly int _failCountBeforeSuccess;
        private int _failuresEmitted;

        public int InvocationCount;

        public FlakyToolExecutor(int failCountBeforeSuccess)
        {
            _failCountBeforeSuccess = failCountBeforeSuccess;
        }

        public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken)
        {
            InvocationCount++;
            if (_failuresEmitted < _failCountBeforeSuccess)
            {
                _failuresEmitted++;
                return Task.FromResult(new ToolExecutionResult(false, new Dictionary<string, string>(), "transient"));
            }

            return Task.FromResult(new ToolExecutionResult(true, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["message"] = "ok"
            }));
        }
    }
}