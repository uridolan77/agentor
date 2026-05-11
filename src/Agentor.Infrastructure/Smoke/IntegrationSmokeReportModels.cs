using System.Text.Json.Serialization;

namespace Agentor.Infrastructure.Smoke;

public sealed class IntegrationSmokeReport
{
    public DateTimeOffset GeneratedAtUtc { get; set; }

    public bool OverallOk { get; set; }

    public List<SmokeStepRecord> Steps { get; set; } = [];
}

public sealed class SmokeStepRecord
{
    public string Target { get; set; } = "";

    public string Name { get; set; } = "";

    public bool Ok { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? HttpStatus { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; set; }
}
