using Agentor.Application.Athanor;
using Agentor.Application.Abstractions;
using Agentor.Contracts.KnowledgeState;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Infrastructure;
using Agentor.Infrastructure.Athanor;
using Xunit;

namespace Agentor.Application.Tests;

public sealed class AthanorCommandHandlersTests
{
    [Fact]
    public async Task GetLatestSnapshot_ReturnsSeededSnapshot_WhenRunExists()
    {
        var repo = new InMemoryAgentRunRepository();
        var ath = new FakeKnowledgeStateClient();
        var clock = new SystemClock();
        var profile = AgentProfile.Create("Agent", "Profile", clock.UtcNow);
        var run = AgentRun.Start(profile.Id, profile.Name, "objective", "trace-1", clock.UtcNow);
        run.StartStep("only", clock.UtcNow);
        await repo.SaveAsync(run, CancellationToken.None);

        var snap = new CanonicalSnapshotDto(Guid.NewGuid(), run.ProfileId, clock.UtcNow, []);
        ath.SeedLatestSnapshot(snap);

        var handler = new GetLatestAthanorSnapshotForRunQueryHandler(repo, ath);
        var result = await handler.HandleAsync(run.Id, CancellationToken.None);
        Assert.True(result.RunExists);
        Assert.NotNull(result.Snapshot);
        Assert.Equal(run.ProfileId, result.Snapshot!.ProjectId);
    }

    [Fact]
    public async Task LookupCanonical_UnknownRun_ReturnsRunNotExists()
    {
        var repo = new InMemoryAgentRunRepository();
        var ath = new FakeKnowledgeStateClient();
        var handler = new LookupAthanorCanonicalForRunQueryHandler(repo, ath);
        var result = await handler.HandleAsync(Guid.NewGuid(), "any-key", CancellationToken.None);
        Assert.False(result.RunExists);
        Assert.Null(result.Entry);
    }

    [Fact]
    public async Task LookupCanonical_EmptyKey_ThrowsArgumentException()
    {
        var repo = new InMemoryAgentRunRepository();
        var ath = new FakeKnowledgeStateClient();
        var clock = new SystemClock();
        var profile = AgentProfile.Create("Agent", "Profile", clock.UtcNow);
        var run = AgentRun.Start(profile.Id, profile.Name, "objective", "trace-lookup", clock.UtcNow);
        await repo.SaveAsync(run, CancellationToken.None);

        var handler = new LookupAthanorCanonicalForRunQueryHandler(repo, ath);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.HandleAsync(run.Id, "   ", CancellationToken.None));
    }

    [Fact]
    public async Task AttachEvidenceProvenance_WritesTrace_WhenRunRunning()
    {
        var repo = new InMemoryAgentRunRepository();
        var ath = new FakeKnowledgeStateClient();
        var clock = new SystemClock();
        var profile = AgentProfile.Create("Agent", "Profile", clock.UtcNow);
        var run = AgentRun.Start(profile.Id, profile.Name, "objective", "trace-2", clock.UtcNow);
        run.StartStep("only", clock.UtcNow);
        await repo.SaveAsync(run, CancellationToken.None);

        var eid = Guid.NewGuid();
        ath.SeedSearchResults(run.ProfileId, "alpha", [new EvidenceSearchResultDto(eid, "t", "s")]);

        var handler = new AttachAthanorEvidenceProvenanceHandler(repo, ath, clock);
        var ok = await handler.HandleAsync(run.Id, "alpha", CancellationToken.None);
        Assert.True(ok);

        var loaded = await repo.GetAsync(run.Id, CancellationToken.None);
        Assert.Contains(loaded!.Trace, e => e.Kind == TraceEventKind.AthanorEvidenceSearchProvenanceAttached);
        var evt = loaded.Trace.Last(e => e.Kind == TraceEventKind.AthanorEvidenceSearchProvenanceAttached);
        Assert.Equal("alpha", evt.Data!["query"]);
        Assert.Contains(eid.ToString("D"), evt.Data["evidenceResultIds"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitCandidate_WhenRunNotRunning_ReturnsFalseWithoutTrace()
    {
        var repo = new InMemoryAgentRunRepository();
        var ath = new FakeKnowledgeStateClient();
        var clock = new SystemClock();
        var profile = AgentProfile.Create("Agent", "Profile", clock.UtcNow);
        var completed = AgentRun.Reconstitute(
            Guid.NewGuid(),
            profile.Id,
            "Agent",
            "objective",
            "trace-done",
            AgentRunStatus.Completed,
            clock.UtcNow.AddMinutes(-5),
            clock.UtcNow,
            null,
            [],
            []);
        await repo.SaveAsync(completed, CancellationToken.None);

        var submit = new SubmitAthanorCandidateHandler(repo, ath, clock);
        var (ok, cid) = await submit.HandleAsync(completed.Id, "sum", "{}", CancellationToken.None);
        Assert.False(ok);
        Assert.Null(cid);

        var loaded = await repo.GetAsync(completed.Id, CancellationToken.None);
        Assert.DoesNotContain(loaded!.Trace, e => e.Kind == TraceEventKind.AthanorCandidateSubmitted);
    }

    [Fact]
    public async Task QueueReview_WhenRunNotRunning_ReturnsFalseWithoutTrace()
    {
        var repo = new InMemoryAgentRunRepository();
        var ath = new FakeKnowledgeStateClient();
        var clock = new SystemClock();
        var profile = AgentProfile.Create("Agent", "Profile", clock.UtcNow);
        var completed = AgentRun.Reconstitute(
            Guid.NewGuid(),
            profile.Id,
            "Agent",
            "objective",
            "trace-q",
            AgentRunStatus.Completed,
            clock.UtcNow.AddMinutes(-5),
            clock.UtcNow,
            null,
            [],
            []);
        await repo.SaveAsync(completed, CancellationToken.None);

        var queue = new QueueAthanorReviewHandler(repo, ath, clock);
        var qok = await queue.HandleAsync(completed.Id, Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        Assert.False(qok);

        var loaded = await repo.GetAsync(completed.Id, CancellationToken.None);
        Assert.DoesNotContain(loaded!.Trace, e => e.Kind == TraceEventKind.AthanorReviewQueued);
    }

    [Fact]
    public async Task SubmitCandidate_ThenQueueReview_RecordsBothTraceKinds()
    {
        var repo = new InMemoryAgentRunRepository();
        var ath = new FakeKnowledgeStateClient();
        var clock = new SystemClock();
        var profile = AgentProfile.Create("Agent", "Profile", clock.UtcNow);
        var run = AgentRun.Start(profile.Id, profile.Name, "objective", "trace-3", clock.UtcNow);
        run.StartStep("only", clock.UtcNow);
        await repo.SaveAsync(run, CancellationToken.None);

        var submit = new SubmitAthanorCandidateHandler(repo, ath, clock);
        var (ok, cid) = await submit.HandleAsync(run.Id, "sum", "{}", CancellationToken.None);
        Assert.True(ok);
        Assert.NotNull(cid);

        var actor = Guid.NewGuid();
        var queue = new QueueAthanorReviewHandler(repo, ath, clock);
        var qok = await queue.HandleAsync(run.Id, cid!.Value, actor, CancellationToken.None);
        Assert.True(qok);

        var loaded = await repo.GetAsync(run.Id, CancellationToken.None);
        Assert.Contains(loaded!.Trace, e => e.Kind == TraceEventKind.AthanorCandidateSubmitted);
        Assert.Contains(loaded.Trace, e => e.Kind == TraceEventKind.AthanorReviewQueued);
    }
}