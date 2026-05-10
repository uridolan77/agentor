using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Agentor.Api.Tests;

public sealed class ObservabilityTests : IClassFixture<WebApplicationFactory<Program>>
{
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
}
