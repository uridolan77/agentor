using Agentor.Infrastructure.HttpResilience;
using Agentor.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.Tests;

public sealed class TransportResilienceRegistryTests
{
    [Fact]
    public void RecordFailure_AtThreshold_OpensCircuit_VisibleInSnapshot()
    {
        var registry = new TransportResilienceRegistry();
        var opts = new StaticMonitor<TransportResilienceOptions>(
            new TransportResilienceOptions
            {
                Enabled = true,
                CircuitFailureThreshold = 3,
                CircuitOpenDurationSeconds = 60,
            });

        for (var i = 0; i < 3; i++)
        {
            registry.RecordFailure("Athanor", opts);
        }

        var snap = registry.GetSnapshot();
        Assert.True(snap.TryGetValue("Athanor", out var s));
        Assert.True(s.CircuitOpen);
        Assert.NotNull(s.CircuitOpenUntilUtc);

        var synthetic = registry.TryGetCircuitOpenSyntheticResponse("Athanor", opts);
        Assert.NotNull(synthetic);
        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, synthetic!.StatusCode);
    }

    [Fact]
    public void RecordSuccess_ClearsCircuit()
    {
        var registry = new TransportResilienceRegistry();
        var opts = new StaticMonitor<TransportResilienceOptions>(
            new TransportResilienceOptions
            {
                Enabled = true,
                CircuitFailureThreshold = 2,
                CircuitOpenDurationSeconds = 60,
            });

        registry.RecordFailure("Mcp", opts);
        registry.RecordFailure("Mcp", opts);
        Assert.True(registry.GetSnapshot()["Mcp"].CircuitOpen);

        registry.RecordSuccess("Mcp", opts);
        var snap = registry.GetSnapshot();
        Assert.True(snap.TryGetValue("Mcp", out var s));
        Assert.False(s.CircuitOpen);
    }

    private sealed class StaticMonitor<T>(T value) : IOptionsMonitor<T>
        where T : class
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable OnChange(Action<T, string?> listener) => new EmptyDisposable();

        private sealed class EmptyDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
