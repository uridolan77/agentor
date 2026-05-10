using System.Collections.Concurrent;
using Agentor.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.HttpResilience;

public sealed class TransportResilienceRegistry
{
    private readonly ConcurrentDictionary<string, ClientCircuitState> _byClient = new(StringComparer.Ordinal);

    public HttpResponseMessage? TryGetCircuitOpenSyntheticResponse(string clientName, IOptionsMonitor<TransportResilienceOptions> options)
    {
        if (!options.CurrentValue.Enabled)
        {
            return null;
        }

        var state = _byClient.GetOrAdd(clientName, _ => new ClientCircuitState());
        lock (state.Sync)
        {
            if (state.CircuitOpenUntilUtc is { } openUntil && DateTimeOffset.UtcNow < openUntil)
            {
                var resp = new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    ReasonPhrase = "agentor_circuit_open",
                };
                return resp;
            }
        }

        return null;
    }

    public void RecordSuccess(string clientName, IOptionsMonitor<TransportResilienceOptions> options)
    {
        if (!options.CurrentValue.Enabled)
        {
            return;
        }

        var state = _byClient.GetOrAdd(clientName, _ => new ClientCircuitState());
        lock (state.Sync)
        {
            state.ConsecutiveFailures = 0;
            state.CircuitOpenUntilUtc = null;
        }
    }

    public void RecordFailure(string clientName, IOptionsMonitor<TransportResilienceOptions> options)
    {
        if (!options.CurrentValue.Enabled)
        {
            return;
        }

        var opt = options.CurrentValue;
        var state = _byClient.GetOrAdd(clientName, _ => new ClientCircuitState());
        lock (state.Sync)
        {
            state.ConsecutiveFailures++;
            if (state.ConsecutiveFailures >= opt.CircuitFailureThreshold)
            {
                state.CircuitOpenUntilUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(opt.CircuitOpenDurationSeconds, 1, 3600));
                state.ConsecutiveFailures = 0;
            }
        }
    }

    public IReadOnlyDictionary<string, TransportResilienceSnapshot> GetSnapshot()
    {
        var dict = new Dictionary<string, TransportResilienceSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in _byClient)
        {
            lock (kv.Value.Sync)
            {
                var open = kv.Value.CircuitOpenUntilUtc is { } u && DateTimeOffset.UtcNow < u;
                dict[kv.Key] = new TransportResilienceSnapshot(open, kv.Value.ConsecutiveFailures, kv.Value.CircuitOpenUntilUtc);
            }
        }

        return dict;
    }

    private sealed class ClientCircuitState
    {
        public object Sync { get; } = new();

        public int ConsecutiveFailures { get; set; }

        public DateTimeOffset? CircuitOpenUntilUtc { get; set; }
    }
}

public sealed record TransportResilienceSnapshot(bool CircuitOpen, int ConsecutiveFailures, DateTimeOffset? CircuitOpenUntilUtc);
