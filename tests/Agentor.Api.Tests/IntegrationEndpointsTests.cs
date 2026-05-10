using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agentor.Application.Abstractions;
using Agentor.Application.Commands;
using Agentor.Application.RunQueue;
using Agentor.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agentor.Api.Tests;

public sealed class IntegrationEndpointsTests
{
    [Fact]
    public async Task GetHealth_IsLivenessOnly_ReturnsOk()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("alive", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetReady_WithFakeIntegrations_ReturnsOk()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetIntegrationsStatus_IncludesModesAndNoSecrets()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/integrations/status");
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadAsStringAsync();
        var lower = raw.ToLowerInvariant();
        Assert.DoesNotContain("apikey", lower);
        Assert.DoesNotContain("password", lower);
        Assert.DoesNotContain("secret", lower);
        Assert.DoesNotContain("bearer", lower);
        Assert.DoesNotContain("authorization", lower);

        var doc = JsonDocument.Parse(raw);
        Assert.True(doc.RootElement.GetProperty("ready").GetBoolean());
        Assert.Equal(
            "Fake",
            doc.RootElement.GetProperty("integrations").GetProperty("athanor").GetProperty("mode").GetString());
    }

    [Fact]
    public async Task GetIntegrationsStatus_DisabledAdapter_HasDisabledDetail()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Agentor:Integrations:Athanor:Mode"] = "Disabled",
                });
            });
        });

        using var client = factory.CreateClient();

        var doc = await client.GetFromJsonAsync<JsonDocument>("/api/v1/integrations/status");
        Assert.NotNull(doc);
        var athanor = doc!.RootElement.GetProperty("integrations").GetProperty("athanor");
        Assert.Equal("Disabled", athanor.GetProperty("mode").GetString());
        Assert.True(athanor.GetProperty("ready").GetBoolean());
        Assert.Equal("disabled", athanor.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task GetReady_HttpModeUnreachable_Returns503()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Agentor:Integrations:Athanor:Mode"] = "Http",
                    ["Agentor:Integrations:Athanor:Http:BaseUrl"] = "http://127.0.0.1:9/",
                    ["Agentor:Integrations:Conexus:Mode"] = "Fake",
                    ["Agentor:Integrations:Mcp:Mode"] = "Fake",
                    ["Agentor:Integrations:ExternalAgents:Mode"] = "Fake",
                });
            });
        });

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task GetReady_HttpModeNonSuccessResponse_Returns503()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Agentor:Integrations:Athanor:Mode"] = "Http",
                    ["Agentor:Integrations:Athanor:Http:BaseUrl"] = "http://127.0.0.1:1/",
                    ["Agentor:Integrations:Conexus:Mode"] = "Fake",
                    ["Agentor:Integrations:Mcp:Mode"] = "Fake",
                    ["Agentor:Integrations:ExternalAgents:Mode"] = "Fake",
                });
            });
            b.ConfigureServices(services =>
            {
                foreach (var d in services.Where(x => x.ServiceType == typeof(IHttpClientFactory)).ToList())
                {
                    services.Remove(d);
                }

                services.AddSingleton<IHttpClientFactory>(new FixedStatusHttpClientFactory(HttpStatusCode.InternalServerError));
            });
        });

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task OpsEndpoints_ReturnReadOnlyStatusWithoutSecrets()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Agentor:RunQueue:ExecutionMode"] = "DurableBackground",
                    ["Agentor:RunWorker:Enabled"] = "false",
                    ["Agentor:OutboxDispatch:Enabled"] = "false",
                });
            });
        });

        using var scope = factory.Services.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IDurableRunQueue>();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        var leases = scope.ServiceProvider.GetRequiredService<IRunExecutionLeaseStore>();

        var workItemId = Guid.NewGuid();
        await queue.EnqueueAsync(
            new RunWorkItem(workItemId, new StartAgentRunCommand("Ops Agent", "Queue visibility test.")),
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        await outbox.AppendAsync(
            new Agentor.Application.Reliability.OutboxMessage(
                Guid.NewGuid(),
                Agentor.Application.Reliability.OutboxMessageKind.Mcp,
                "{}",
                Agentor.Application.Reliability.OutboxStatus.Pending,
                0,
                DateTimeOffset.UtcNow,
                null),
            CancellationToken.None);

        await leases.TryAcquireAsync(
            Guid.NewGuid(),
            "ops-test-holder",
            TimeSpan.FromMinutes(1),
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        using var client = factory.CreateClient();

        var queueResponse = await client.GetAsync("/api/v1/ops/queue");
        var outboxResponse = await client.GetAsync("/api/v1/ops/outbox");
        var leasesResponse = await client.GetAsync("/api/v1/ops/leases");

        queueResponse.EnsureSuccessStatusCode();
        outboxResponse.EnsureSuccessStatusCode();
        leasesResponse.EnsureSuccessStatusCode();

        var queueRaw = await queueResponse.Content.ReadAsStringAsync();
        var outboxRaw = await outboxResponse.Content.ReadAsStringAsync();
        var leasesRaw = await leasesResponse.Content.ReadAsStringAsync();

        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var queueItems = JsonSerializer.Deserialize<List<OpsQueueItemDto>>(queueRaw, jsonOptions);
        var outboxItems = JsonSerializer.Deserialize<List<OpsOutboxItemDto>>(outboxRaw, jsonOptions);
        var leaseItems = JsonSerializer.Deserialize<List<OpsLeaseItemDto>>(leasesRaw, jsonOptions);

        Assert.NotNull(queueItems);
        Assert.Contains(queueItems!, x => x.WorkItemId == workItemId);
        Assert.NotNull(outboxItems);
        Assert.NotEmpty(outboxItems!);
        Assert.NotNull(leaseItems);
        Assert.NotEmpty(leaseItems!);

        var raw = string.Concat(queueRaw, outboxRaw, leasesRaw).ToLowerInvariant();
        Assert.DoesNotContain("password", raw);
        Assert.DoesNotContain("secret", raw);
        Assert.DoesNotContain("authorization", raw);
        Assert.DoesNotContain("apikey", raw);
    }

    private sealed class FixedStatusHttpClientFactory(HttpStatusCode statusCode) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(new LambdaMessageHandler((_, _) =>
                    Task.FromResult(new HttpResponseMessage(statusCode))))
            {
                BaseAddress = new Uri("http://probe.local/"),
            };
    }

    private sealed class LambdaMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
