using Agentor.Contracts.KnowledgeState;
using Agentor.Infrastructure.Athanor;

namespace Agentor.Infrastructure.Tests;

public sealed class FakeKnowledgeStateClientTests
{
    [Fact]
    public async Task GetLatestSnapshotAsync_ReturnsNull_ByDefault()
    {
        var sut = new FakeKnowledgeStateClient();
        var snap = await sut.GetLatestSnapshotAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Null(snap);
    }

    [Fact]
    public async Task SearchEvidenceAsync_ReturnsEmpty_ByDefault()
    {
        var sut = new FakeKnowledgeStateClient();
        var hits = await sut.SearchEvidenceAsync(Guid.NewGuid(), "any", CancellationToken.None);
        Assert.Empty(hits);
    }

    [Fact]
    public async Task SubmitCandidateAsync_ReturnsCandidateId_NonCanonStatus()
    {
        var sut = new FakeKnowledgeStateClient();
        var pid = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var r = await sut.SubmitCandidateAsync(
            pid,
            runId,
            new CandidateKnowledgeSubmissionDto("s", "{}"),
            CancellationToken.None);
        Assert.NotEqual(Guid.Empty, r.CandidateId);
        Assert.Contains("non_canon", r.Status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueueForReviewAsync_ReturnsReviewItem()
    {
        var sut = new FakeKnowledgeStateClient();
        var r = await sut.QueueForReviewAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        Assert.NotEqual(Guid.Empty, r.ReviewItemId);
    }

    [Fact]
    public async Task LookupCanonicalEntryAsync_UsesSeededSnapshot()
    {
        var sut = new FakeKnowledgeStateClient();
        var pid = Guid.NewGuid();
        var snap = new CanonicalSnapshotDto(
            Guid.NewGuid(),
            pid,
            DateTimeOffset.UtcNow,
            [new CanonicalStateEntryDto("k1", "v1", 1.0)]);
        sut.SeedLatestSnapshot(snap);
        var entry = await sut.LookupCanonicalEntryAsync(pid, "k1", CancellationToken.None);
        Assert.NotNull(entry);
        Assert.Equal("v1", entry!.Value);
    }
}