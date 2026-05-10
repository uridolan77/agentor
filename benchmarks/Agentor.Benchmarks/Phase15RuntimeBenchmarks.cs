using BenchmarkDotNet.Attributes;
using Agentor.Application;
using Agentor.Application.Manifest;
using Agentor.Application.Options;
using Agentor.Application.Queries;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Infrastructure;
using Microsoft.Extensions.Options;

namespace Agentor.Benchmarks;

[MemoryDiagnoser]
public class Phase15RuntimeBenchmarks
{
    private InMemoryAgentRunRepository _repo = null!;
    private AgentRun _run = null!;
    private GetRunAuditExportQueryHandler _auditHandler = null!;

    [GlobalSetup]
    public void Setup()
    {
        var clock = new SystemClock();
        var now = clock.UtcNow;
        _run = AgentRun.Start(Guid.NewGuid(), "BenchAgent", "Objective", "bench-trace", now);
        var step = _run.StartStep("Step", now);
        var tool = ToolCall.Start(_run.Id, step.Id, WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>(), now);
        step.AddToolCall(tool);
        tool.Succeed(new Dictionary<string, string> { ["result"] = "ok" }, now);
        step.Complete(now);
        _run.Complete(now);
        _repo = new InMemoryAgentRunRepository();
        _repo.SaveAsync(_run, CancellationToken.None).GetAwaiter().GetResult();
        _auditHandler = new GetRunAuditExportQueryHandler(
            _repo,
            Options.Create(new AuditExportOptions()));
    }

    [Benchmark]
    public async Task AuditExport_HandleAsync()
    {
        await _auditHandler.HandleAsync(_run.Id, CancellationToken.None);
    }

    [Benchmark]
    public RunManifest Manifest_FromRun()
    {
        return RunManifest.FromRun(_run, ModelCallTelemetryAggregator.Aggregate(_run), ExternalAgentTelemetryAggregator.Aggregate(_run));
    }
}