using Agentor.Application.Observability;

namespace Agentor.Infrastructure.Http;

/// <summary>
/// Propagates the inbound request correlation id to integration HTTP calls as <c>X-Agentor-Trace-Id</c>.
/// </summary>
internal sealed class CorrelationHeadersDelegatingHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (AgentorCorrelationContext.Current is { } id
            && !request.Headers.Contains("X-Agentor-Trace-Id"))
        {
            request.Headers.TryAddWithoutValidation("X-Agentor-Trace-Id", id);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
