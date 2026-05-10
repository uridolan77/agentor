using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Domain.Tests;

public sealed class RunManifestTests
{
    private static AgentRun BuildCompletedRun(string traceId = "test-trace")
    {
        var run = AgentRun.Start(Guid.NewGuid(), "Hash Agent", "Test objective.", traceId, DateTimeOffset.UtcNow);
        var step = run.StartStep("step-1", DateTimeOffset.UtcNow);
        step.Complete(DateTimeOffset.UtcNow);
        run.Complete(DateTimeOffset.UtcNow);
        return run;
    }

    [Fact]
    public void FromRun_SetsManifestVersionTo_1_0()
    {
        var run = BuildCompletedRun();
        var manifest = RunManifest.FromRun(run);

        Assert.Equal(RunManifest.CurrentVersion, manifest.ManifestVersion);
        Assert.Equal("1.0", manifest.ManifestVersion);
    }

    [Fact]
    public void FromRun_ProducesNonEmptyContentHash()
    {
        var run = BuildCompletedRun();
        var manifest = RunManifest.FromRun(run);

        Assert.False(string.IsNullOrWhiteSpace(manifest.ContentHash));
    }

    [Fact]
    public void FromRun_ContentHash_IsHexString()
    {
        var run = BuildCompletedRun();
        var manifest = RunManifest.FromRun(run);

        Assert.Matches("^[0-9a-f]{64}$", manifest.ContentHash);
    }

    [Fact]
    public void FromRun_SameRunProducesSameHash()
    {
        var run = BuildCompletedRun("determinism-trace");

        var manifest1 = RunManifest.FromRun(run);
        var manifest2 = RunManifest.FromRun(run);

        Assert.Equal(manifest1.ContentHash, manifest2.ContentHash);
    }

    [Fact]
    public void ComputeContentHash_DifferentTraceIds_ProduceDifferentHashes()
    {
        var now = DateTimeOffset.UtcNow;
        var runId = Guid.NewGuid();
        var profileId = Guid.NewGuid();

        var hash1 = RunManifest.ComputeContentHash(
            runId, profileId, "trace-a", AgentRunStatus.Completed,
            now, now.AddSeconds(1), 1, 1, 1, 5);

        var hash2 = RunManifest.ComputeContentHash(
            runId, profileId, "trace-b", AgentRunStatus.Completed,
            now, now.AddSeconds(1), 1, 1, 1, 5);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeContentHash_DifferentStepCounts_ProduceDifferentHashes()
    {
        var now = DateTimeOffset.UtcNow;
        var runId = Guid.NewGuid();
        var profileId = Guid.NewGuid();

        var hash1 = RunManifest.ComputeContentHash(
            runId, profileId, "t", AgentRunStatus.Completed,
            now, now.AddSeconds(1), 1, 1, 1, 5);

        var hash2 = RunManifest.ComputeContentHash(
            runId, profileId, "t", AgentRunStatus.Completed,
            now, now.AddSeconds(1), 2, 1, 1, 5);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeContentHash_DifferentStatus_ProduceDifferentHashes()
    {
        var now = DateTimeOffset.UtcNow;
        var runId = Guid.NewGuid();
        var profileId = Guid.NewGuid();

        var hash1 = RunManifest.ComputeContentHash(
            runId, profileId, "t", AgentRunStatus.Completed,
            now, now.AddSeconds(1), 1, 1, 1, 5);

        var hash2 = RunManifest.ComputeContentHash(
            runId, profileId, "t", AgentRunStatus.Failed,
            now, now.AddSeconds(1), 1, 1, 1, 5);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeContentHash_NullCompletedAt_ProducesStableHash()
    {
        var now = DateTimeOffset.UtcNow;
        var runId = Guid.NewGuid();
        var profileId = Guid.NewGuid();

        var hash1 = RunManifest.ComputeContentHash(
            runId, profileId, "t", AgentRunStatus.Running,
            now, null, 0, 0, 0, 1);

        var hash2 = RunManifest.ComputeContentHash(
            runId, profileId, "t", AgentRunStatus.Running,
            now, null, 0, 0, 0, 1);

        Assert.Equal(hash1, hash2);
    }
}
