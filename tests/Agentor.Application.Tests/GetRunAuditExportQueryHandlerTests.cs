using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Agentor.Application;
using Agentor.Application.Options;
using Agentor.Application.Queries;
using Agentor.Contracts;
using Agentor.Infrastructure;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;
using Microsoft.Extensions.Options;
using Xunit;

namespace Agentor.Application.Tests;

public sealed class GetRunAuditExportQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_SameRunTwice_ReturnsIdenticalCanonicalJsonAndHash()
    {
        var repo = new InMemoryAgentRunRepository();
        var clock = new SystemClock();
        var now = clock.UtcNow;
        var run = BuildSimpleCompletedRun(now);
        await repo.SaveAsync(run, CancellationToken.None);

        var handler = new GetRunAuditExportQueryHandler(repo, Microsoft.Extensions.Options.Options.Create(new AuditExportOptions()));
        var first = await handler.HandleAsync(run.Id, CancellationToken.None);
        var second = await handler.HandleAsync(run.Id, CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first!.CanonicalJson, second!.CanonicalJson);
        Assert.Equal(first.ContentSha256Hex, second.ContentSha256Hex);
        var expectedHex = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(first.CanonicalJson)));
        Assert.Equal(expectedHex, first.ContentSha256Hex);
    }

    [Fact]
    public async Task HandleAsync_IncludesEffectivePolicyScopeObject()
    {
        var repo = new InMemoryAgentRunRepository();
        var clock = new SystemClock();
        var run = BuildSimpleCompletedRun(clock.UtcNow);
        await repo.SaveAsync(run, CancellationToken.None);

        var handler = new GetRunAuditExportQueryHandler(repo, Microsoft.Extensions.Options.Options.Create(new AuditExportOptions()));
        var result = await handler.HandleAsync(run.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("effectivePolicyScope", result!.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("knowledgeScopeId", result.CanonicalJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_RedactsPropertyNamesMatchingSensitiveSubstrings()
    {
        var repo = new InMemoryAgentRunRepository();
        var clock = new SystemClock();
        var now = clock.UtcNow;
        var run = AgentRun.Start(Guid.NewGuid(), "Agent", "Objective", "trace-redact", now);
        var step = run.StartStep("Step", now);
        var tool = ToolCall.Start(
            run.Id,
            step.Id,
            WellKnownToolKeys.Pr1FakeTool,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["myApiKey"] = "super-secret",
                ["password"] = "hunter2",
                ["safeField"] = "visible"
            },
            now);
        step.AddToolCall(tool);
        tool.Succeed(new Dictionary<string, string> { ["ok"] = "1" }, now);
        step.Complete(now);
        run.Complete(now);
        await repo.SaveAsync(run, CancellationToken.None);

        var handler = new GetRunAuditExportQueryHandler(repo, Microsoft.Extensions.Options.Options.Create(new AuditExportOptions()));
        var result = await handler.HandleAsync(run.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("[REDACTED]", result!.CanonicalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", result.CanonicalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", result.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("visible", result.CanonicalJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_RedactsNestedSecrets_InToolStructuredIoBody()
    {
        var repo = new InMemoryAgentRunRepository();
        var clock = new SystemClock();
        var now = clock.UtcNow;
        var run = AgentRun.Start(Guid.NewGuid(), "Agent", "Objective", "trace-nested-redact", now);
        var step = run.StartStep("Step", now);
        var body = new JsonObject
        {
            ["settings"] = new JsonObject { ["apiKey"] = "nested-deep-secret" },
            ["prompt"] = "hello"
        };
        var tool = ToolCall.Start(
            run.Id,
            step.Id,
            WellKnownToolKeys.Pr1FakeTool,
            new ToolPayload(body, null, null, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["stableSummary"] = "unchanged" }),
            now);
        step.AddToolCall(tool);
        tool.Succeed(ToolPayload.FromLegacyDictionary(new Dictionary<string, string> { ["ok"] = "1" }), now);
        step.Complete(now);
        run.Complete(now);
        await repo.SaveAsync(run, CancellationToken.None);

        var handler = new GetRunAuditExportQueryHandler(repo, Microsoft.Extensions.Options.Options.Create(new AuditExportOptions()));
        var result = await handler.HandleAsync(run.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("[REDACTED]", result!.CanonicalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("nested-deep-secret", result.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("unchanged", result.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("hello", result.CanonicalJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_IncludesHumanReviewWorkflowChain_InCanonicalJson()
    {
        var repo = new InMemoryAgentRunRepository();
        var clock = new SystemClock();
        var now = clock.UtcNow;
        var priorActor = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var run = AgentRun.Start(Guid.NewGuid(), "Agent", "Objective", "audit-chain", now);
        var step = run.StartStep("Step", now);
        var tool = ToolCall.Start(run.Id, step.Id, WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>(), now);
        step.AddToolCall(tool);
        tool.MarkRequiresReview("policy", now);
        step.MarkRequiresReview(now);
        run.EnterRequiresReview("policy", now);

        run.ApplyHumanReviewDecision(
            new HumanReviewDecision(
                Guid.NewGuid(),
                ReviewDecisionKind.RequestChanges,
                priorActor,
                now,
                "Revise wording.",
                ReviewResolutionStatus.ChangesRequested),
            now);

        run.ApplyHumanReviewDecision(
            new HumanReviewDecision(
                Guid.NewGuid(),
                ReviewDecisionKind.Escalate,
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                now.AddSeconds(1),
                "Senior review.",
                ReviewResolutionStatus.Escalated,
                priorActor),
            now.AddSeconds(1));

        await repo.SaveAsync(run, CancellationToken.None);

        var handler = new GetRunAuditExportQueryHandler(repo, Microsoft.Extensions.Options.Options.Create(new AuditExportOptions()));
        var result = await handler.HandleAsync(run.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("reviewWorkflowStatus", result!.CanonicalJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("relatedPriorActorId", result.CanonicalJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Escalated", result.CanonicalJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_PrettyFormat_PreservesCanonicalJsonAndHash()
    {
        var repo = new InMemoryAgentRunRepository();
        var clock = new SystemClock();
        var run = BuildSimpleCompletedRun(clock.UtcNow);
        await repo.SaveAsync(run, CancellationToken.None);

        var handler = new GetRunAuditExportQueryHandler(repo, Microsoft.Extensions.Options.Options.Create(new AuditExportOptions()));
        var canonical = await handler.HandleAsync(run.Id, AuditExportFormatKind.Canonical, CancellationToken.None);
        var pretty = await handler.HandleAsync(run.Id, AuditExportFormatKind.Pretty, CancellationToken.None);

        Assert.NotNull(canonical);
        Assert.NotNull(pretty);
        Assert.NotEqual(canonical!.ResponseBody, pretty!.ResponseBody);
        Assert.Equal(canonical.CanonicalJson, pretty.CanonicalJson);
        Assert.Equal(canonical.ContentSha256Hex, pretty.ContentSha256Hex);
    }

    [Fact]
    public async Task HandleAsync_HashOnlyFormat_ContainsCanonicalHex()
    {
        var repo = new InMemoryAgentRunRepository();
        var clock = new SystemClock();
        var run = BuildSimpleCompletedRun(clock.UtcNow);
        await repo.SaveAsync(run, CancellationToken.None);

        var handler = new GetRunAuditExportQueryHandler(repo, Microsoft.Extensions.Options.Options.Create(new AuditExportOptions()));
        var canonical = await handler.HandleAsync(run.Id, AuditExportFormatKind.Canonical, CancellationToken.None);
        var hashOnly = await handler.HandleAsync(run.Id, AuditExportFormatKind.HashOnly, CancellationToken.None);

        Assert.NotNull(canonical);
        Assert.NotNull(hashOnly);
        Assert.Contains(canonical!.ContentSha256Hex, hashOnly!.ResponseBody, StringComparison.Ordinal);
        Assert.Equal(canonical.ContentSha256Hex, hashOnly.ContentSha256Hex);
    }

    [Fact]
    public async Task HandleAsync_RedactionReport_ListsPathsAndMatchesCanonicalHash()
    {
        var repo = new InMemoryAgentRunRepository();
        var clock = new SystemClock();
        var now = clock.UtcNow;
        var run = AgentRun.Start(Guid.NewGuid(), "Agent", "Objective", "trace-redact-report", now);
        var step = run.StartStep("Step", now);
        var tool = ToolCall.Start(
            run.Id,
            step.Id,
            WellKnownToolKeys.Pr1FakeTool,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["apiKey"] = "secret-token" },
            now);
        step.AddToolCall(tool);
        tool.Succeed(new Dictionary<string, string> { ["ok"] = "1" }, now);
        step.Complete(now);
        run.Complete(now);
        await repo.SaveAsync(run, CancellationToken.None);

        var handler = new GetRunAuditExportQueryHandler(repo, Microsoft.Extensions.Options.Options.Create(new AuditExportOptions()));
        var canonical = await handler.HandleAsync(run.Id, AuditExportFormatKind.Canonical, CancellationToken.None);
        var report = await handler.HandleAsync(run.Id, AuditExportFormatKind.RedactionReport, CancellationToken.None);

        Assert.NotNull(canonical);
        Assert.NotNull(report);
        Assert.Contains("redactedKeyPaths", report!.ResponseBody, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(canonical!.ContentSha256Hex, report.ContentSha256Hex);
    }

    private static AgentRun BuildSimpleCompletedRun(DateTimeOffset now)
    {
        var run = AgentRun.Start(Guid.NewGuid(), "Agent", "Objective", "trace-audit", now);
        var step = run.StartStep("Step", now);
        var tool = ToolCall.Start(run.Id, step.Id, WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>(), now);
        step.AddToolCall(tool);
        tool.Succeed(new Dictionary<string, string> { ["result"] = "ok" }, now);
        step.Complete(now);
        run.Complete(now);
        return run;
    }
}
