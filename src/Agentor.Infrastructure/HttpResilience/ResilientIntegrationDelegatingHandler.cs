using Agentor.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.HttpResilience;

/// <summary>
/// Retries and opens a circuit for named integration HttpClient instances.
/// Does not change application-level tool pipeline retry semantics.
/// When resilience is enabled, each outbound attempt uses a cloned HttpRequestMessage so
/// retries never resend the same HttpContent instance (POST bodies are buffered once).
/// HttpRequestOptions entries on the inbound message are not copied to clones (integration clients do not rely on them today).
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

        byte[]? bufferedBody = null;
        if (request.Content is not null)
        {
            bufferedBody = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }

        var maxAttempts = 1 + Math.Clamp(opts.MaxRetries, 0, 10);
        HttpResponseMessage? disposeNext = null;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            disposeNext?.Dispose();
            disposeNext = null;

            using var attemptRequest = CloneHttpRequestMessage(request, bufferedBody);

            var response = await base.SendAsync(attemptRequest, cancellationToken).ConfigureAwait(false);
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

    private static HttpRequestMessage CloneHttpRequestMessage(HttpRequestMessage source, byte[]? bufferedBody)
    {
        var clone = new HttpRequestMessage(source.Method, source.RequestUri)
        {
            Version = source.Version,
            VersionPolicy = source.VersionPolicy,
        };

        foreach (var header in source.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (bufferedBody is not null)
        {
            var content = new ByteArrayContent(bufferedBody);
            if (source.Content is not null)
            {
                foreach (var header in source.Content.Headers)
                {
                    content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            clone.Content = content;
        }

        return clone;
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