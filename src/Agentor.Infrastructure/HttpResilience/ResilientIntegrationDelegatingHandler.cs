using Agentor.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.HttpResilience;

/// <summary>
/// Retries and opens a circuit for named integration <see cref="HttpClient"/> instances.
/// Does not change <see cref="Agentor.Application.Abstractions.IToolExecutionPipeline"/> retry semantics.
/// </summary>
public sealed class ResilientIntegrationDelegatingHandler : DelegatingHandler
{
    private readonly string _clientName;
    private readonly TransportResilienceRegistry _registry;
    private readonly IOptionsMonitor<TransportResilienceOptions> _options;

    public ResilientIntegrationDelegatingHandler(
        string clientName,
        TransportResilienceRegistry registry,
        IOptionsMonitor<TransportResilienceOptions> options)
    {
        _clientName = clientName;
        _registry = registry;
        _options = options;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled || InnerHandler is null)
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var blocked = _registry.TryGetCircuitOpenSyntheticResponse(_clientName, _options);
        if (blocked is not null)
        {
            return blocked;
        }

        var maxAttempts = 1 + Math.Clamp(opts.MaxRetries, 0, 10);
        HttpResponseMessage? disposeNext = null;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            disposeNext?.Dispose();
            disposeNext = null;

            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                _registry.RecordSuccess(_clientName, _options);
                return response;
            }

            if (!ShouldRetry(response.StatusCode, opts))
            {
                _registry.RecordFailure(_clientName, _options);
                return response;
            }

            if (attempt == maxAttempts - 1)
            {
                _registry.RecordFailure(_clientName, _options);
                return response;
            }

            disposeNext = response;
            var delay = TimeSpan.FromMilliseconds(opts.BaseBackoffMilliseconds * (attempt + 1));
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
    }

    private static bool ShouldRetry(System.Net.HttpStatusCode code, TransportResilienceOptions opts)
    {
        var v = (int)code;
        foreach (var c in opts.RetryableStatusCodes ?? [])
        {
            if (c == v)
            {
                return true;
            }
        }

        return false;
    }
}
