using System.Diagnostics.Metrics;
using Agentor.Application.Abstractions;
using Agentor.Application.Reliability;
using Agentor.Application.RunQueue;
using Agentor.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Agentor.Infrastructure.Observability;

public sealed class AgentorRuntimeMetricsRecorder : IRuntimeMetricsRecorder
{
    public const string MeterName = "Agentor.Runtime";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    private readonly IServiceScopeFactory _scopeFactory;

    private readonly Counter<long> _runsStarted = Meter.CreateCounter<long>("agentor.runs.started");
    private readonly Counter<long> _runsCompleted = Meter.CreateCounter<long>("agentor.runs.completed");
    private readonly Counter<long> _runsFailed = Meter.CreateCounter<long>("agentor.runs.failed");
    private readonly Counter<long> _runsRequiresReview = Meter.CreateCounter<long>("agentor.runs.requires_review");

    private readonly Counter<long> _policyAllowed = Meter.CreateCounter<long>("agentor.policy.allowed");
    private readonly Counter<long> _policyDenied = Meter.CreateCounter<long>("agentor.policy.denied");
    private readonly Counter<long> _policyRequiresReview = Meter.CreateCounter<long>("agentor.policy.requires_review");

    private readonly Counter<long> _toolsStarted = Meter.CreateCounter<long>("agentor.tools.started");
    private readonly Counter<long> _toolsCompleted = Meter.CreateCounter<long>("agentor.tools.completed");
    private readonly Counter<long> _toolsFailed = Meter.CreateCounter<long>("agentor.tools.failed");

    private readonly Counter<long> _queueClaimed = Meter.CreateCounter<long>("agentor.queue.claimed");
    private readonly Counter<long> _queueCompleted = Meter.CreateCounter<long>("agentor.queue.completed");
    private readonly Counter<long> _queueFailed = Meter.CreateCounter<long>("agentor.queue.failed");

    private readonly Counter<long> _outboxDispatchStarted = Meter.CreateCounter<long>("agentor.outbox.dispatch.started");
    private readonly Counter<long> _outboxDispatchCompleted = Meter.CreateCounter<long>("agentor.outbox.dispatch.completed");
    private readonly Counter<long> _outboxDispatchFailed = Meter.CreateCounter<long>("agentor.outbox.dispatch.failed");

    private readonly Counter<long> _integrationErrors = Meter.CreateCounter<long>("agentor.integrations.errors");

    public AgentorRuntimeMetricsRecorder(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;

        Meter.CreateObservableGauge(
            "agentor.queue.depth",
            () => new Measurement<int>(MeasureQueueDepth()),
            unit: "{items}",
            description: "Approximate durable queue items not yet completed (pending + claimed).");

        Meter.CreateObservableGauge(
            "agentor.outbox.pending",
            () => new Measurement<int>(MeasureOutboxPending()),
            unit: "{items}",
            description: "Approximate outbox messages awaiting dispatch (pending + dispatching).");
    }

    private int MeasureQueueDepth()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var queue = scope.ServiceProvider.GetRequiredService<IDurableRunQueue>();
            var rows = queue.ListLatestAsync(5000, CancellationToken.None).GetAwaiter().GetResult();
            return rows.Count(static r =>
                r.Status is DurableRunQueueStatus.Pending or DurableRunQueueStatus.Claimed);
        }
        catch
        {
            return 0;
        }
    }

    private int MeasureOutboxPending()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var outbox = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
            var rows = outbox.ListLatestAsync(5000, CancellationToken.None).GetAwaiter().GetResult();
            return rows.Count(static r =>
                r.Status is OutboxStatus.Pending or OutboxStatus.Dispatching);
        }
        catch
        {
            return 0;
        }
    }

    public void RecordRunStarted() => _runsStarted.Add(1);

    public void RecordRunCompleted() => _runsCompleted.Add(1);

    public void RecordRunFailed() => _runsFailed.Add(1);

    public void RecordRunRequiresReview() => _runsRequiresReview.Add(1);

    public void RecordPolicyAllowed() =>
        _policyAllowed.Add(1, new KeyValuePair<string, object?>("policyEffect", nameof(PolicyDecisionOutcome.Allow)));

    public void RecordPolicyDenied() =>
        _policyDenied.Add(1, new KeyValuePair<string, object?>("policyEffect", nameof(PolicyDecisionOutcome.Deny)));

    public void RecordPolicyRequiresReview() =>
        _policyRequiresReview.Add(1, new KeyValuePair<string, object?>("policyEffect", nameof(PolicyDecisionOutcome.RequiresReview)));

    public void RecordToolStarted(string toolKey) =>
        _toolsStarted.Add(1, new KeyValuePair<string, object?>("toolKey", SafeDimension(toolKey)));

    public void RecordToolCompleted(string toolKey) =>
        _toolsCompleted.Add(1, new KeyValuePair<string, object?>("toolKey", SafeDimension(toolKey)));

    public void RecordToolFailed(string toolKey) =>
        _toolsFailed.Add(1, new KeyValuePair<string, object?>("toolKey", SafeDimension(toolKey)));

    public void RecordQueueClaimed() => _queueClaimed.Add(1);

    public void RecordQueueCompleted() => _queueCompleted.Add(1);

    public void RecordQueueFailed() => _queueFailed.Add(1);

    public void RecordOutboxDispatchStarted() => _outboxDispatchStarted.Add(1);

    public void RecordOutboxDispatchCompleted() => _outboxDispatchCompleted.Add(1);

    public void RecordOutboxDispatchFailed() => _outboxDispatchFailed.Add(1);

    public void RecordIntegrationError(string integrationName) =>
        _integrationErrors.Add(1, new KeyValuePair<string, object?>("integrationName", SafeDimension(integrationName)));

    private static string SafeDimension(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var t = value.Trim();
        return t.Length <= 128 ? t : t[..128];
    }
}
