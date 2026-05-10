using System.Net;
using System.Net.Http.Headers;
using Agentor.Infrastructure.HttpResilience;
using Agentor.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.Tests;

public sealed class ResilientIntegrationDelegatingHandlerTests
{
    [Fact]
    public async Task PostJson_RetriesWithFreshRequestMessage_EachAttemptHasSameBody()
    {
        var json = "{\"a\":1}"u8.ToArray();
        var inner = new QueueStatusHandler([HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK]);
        var opts = new TransportResilienceOptions { Enabled = true, MaxRetries = 2, BaseBackoffMilliseconds = 1 };
        var handler = new ResilientIntegrationDelegatingHandler(
            "client",
            new TransportResilienceRegistry(),
            new FixedOptionsMonitor(opts))
        {
            InnerHandler = inner,
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://example.test/path");
        req.Content = new ByteArrayContent(json);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var resp = await new HttpMessageInvoker(handler).SendAsync(req, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(3, inner.SendCount);
        Assert.Equal(3, inner.Bodies.Count);
        Assert.All(inner.Bodies, b => Assert.Equal(json, b));
    }

    [Fact]
    public async Task MaxRetries_Stops_AtConfiguredAttempts()
    {
        var inner = new QueueStatusHandler([HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable]);
        var opts = new TransportResilienceOptions { Enabled = true, MaxRetries = 1, BaseBackoffMilliseconds = 1 };
        var handler = new ResilientIntegrationDelegatingHandler(
            "client",
            new TransportResilienceRegistry(),
            new FixedOptionsMonitor(opts))
        {
            InnerHandler = inner,
        };

        using var req = new HttpRequestMessage(HttpMethod.Get, "https://example.test/");
        using var resp = await new HttpMessageInvoker(handler).SendAsync(req, CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode);
        Assert.Equal(2, inner.SendCount);
    }

    [Fact]
    public async Task NonRetryableStatus_DoesNotRetry()
    {
        var inner = new QueueStatusHandler([HttpStatusCode.BadRequest]);
        var opts = new TransportResilienceOptions { Enabled = true, MaxRetries = 5, BaseBackoffMilliseconds = 1 };
        var handler = new ResilientIntegrationDelegatingHandler(
            "client",
            new TransportResilienceRegistry(),
            new FixedOptionsMonitor(opts))
        {
            InnerHandler = inner,
        };

        using var req = new HttpRequestMessage(HttpMethod.Get, "https://example.test/");
        using var resp = await new HttpMessageInvoker(handler).SendAsync(req, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal(1, inner.SendCount);
    }

    [Fact]
    public async Task CircuitOpen_ReturnsSynthetic503_WithoutCallingInner()
    {
        var opts = new TransportResilienceOptions
        {
            Enabled = true,
            CircuitFailureThreshold = 2,
            CircuitOpenDurationSeconds = 300,
            BaseBackoffMilliseconds = 1,
            MaxRetries = 0,
        };
        var mon = new FixedOptionsMonitor(opts);
        var registry = new TransportResilienceRegistry();
        registry.RecordFailure("c", mon);
        registry.RecordFailure("c", mon);

        var inner = new ThrowingHandler();
        var handler = new ResilientIntegrationDelegatingHandler("c", registry, mon) { InnerHandler = inner };

        using var req = new HttpRequestMessage(HttpMethod.Get, "https://example.test/");
        using var resp = await new HttpMessageInvoker(handler).SendAsync(req, CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode);
        Assert.Equal("agentor_circuit_open", resp.ReasonPhrase);
        Assert.Equal(0, inner.Invocations);
    }

    private sealed class QueueStatusHandler : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _queue;

        public QueueStatusHandler(IEnumerable<HttpStatusCode> sequence) =>
            _queue = new Queue<HttpStatusCode>(sequence);

        public int SendCount { get; private set; }

        public List<byte[]> Bodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SendCount++;
            if (request.Content is not null)
            {
                Bodies.Add(await request.Content.ReadAsByteArrayAsync(cancellationToken));
            }

            var code = _queue.Dequeue();
            return new HttpResponseMessage(code);
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        public int Invocations { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Invocations++;
            throw new InvalidOperationException("inner should not run when circuit is open");
        }
    }

    private sealed class FixedOptionsMonitor : IOptionsMonitor<TransportResilienceOptions>
    {
        public FixedOptionsMonitor(TransportResilienceOptions value) => CurrentValue = value;

        public TransportResilienceOptions CurrentValue { get; }

        public TransportResilienceOptions Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<TransportResilienceOptions, string?> listener) => NullDisposable.Instance;
    }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}