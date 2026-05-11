using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agentor.Application;
using Agentor.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Agentor.Api.Tests;

public sealed class ObservabilityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly WebApplicationFactory<Program> _factory;

    public ObservabilityTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_request_records_http_server_counter_measurement()
    {
        long total = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "Agentor.Api" && instrument.Name == "agentor.http.server.request.count")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "agentor.http.server.request.count")
            {
                Interlocked.Add(ref total, measurement);
            }
        });

        listener.Start();

        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();

        listener.RecordObservableInstruments();
        Assert.True(total >= 1, "Expected at least one HTTP request counter increment for /health.");
    }

    [Fact]
    public async Task PostAgentRuns_response_includes_request_and_run_trace_headers()
    {
        using var client = _factory.CreateClient();
        var body = new StartAgentRunRequestDto(
            "Observability test agent",
            "Objective text must not appear in response headers.",
            "run-trace-obs-test",
            ToolKey: WellKnownToolKeys.Pr1FakeTool);

        var res = await client.PostAsJsonAsync("/api/v1/agent-runs", body, JsonOptions);
        res.EnsureSuccessStatusCode();

        Assert.True(res.Headers.TryGetValues("X-Agentor-Trace-Id", out var requestTrace));
        Assert.NotNull(requestTrace);
        Assert.NotEmpty(requestTrace.First());

        Assert.True(res.Headers.TryGetValues("X-Agentor-Run-Trace-Id", out var runTrace));
        Assert.Equal("run-trace-obs-test", runTrace!.First());
    }

    [Fact]
    public async Task PostAgentRuns_increments_runtime_runs_started_counter()
    {
        long started = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "Agentor.Runtime" && instrument.Name == "agentor.runs.started")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "agentor.runs.started")
            {
                Interlocked.Add(ref started, measurement);
            }
        });

        listener.Start();

        using var client = _factory.CreateClient();
        var body = new StartAgentRunRequestDto(
            "Counter test",
            "x",
            "counter-trace",
            ToolKey: WellKnownToolKeys.Pr1FakeTool);
        var res = await client.PostAsJsonAsync("/api/v1/agent-runs", body, JsonOptions);
        res.EnsureSuccessStatusCode();

        listener.RecordObservableInstruments();
        Assert.True(started >= 1, "Expected agentor.runs.started counter increment.");
    }
}
