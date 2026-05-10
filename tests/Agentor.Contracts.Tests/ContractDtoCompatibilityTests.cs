using System.Text.Json;
using Agentor.Contracts;
using Agentor.Domain.Enums;
using Xunit;

namespace Agentor.Contracts.Tests;

public sealed class ContractDtoCompatibilityTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void StartAgentRunRequestDto_round_trips_json()
    {
        var original = new StartAgentRunRequestDto("Agent", "Goal", "tid", Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var back = JsonSerializer.Deserialize<StartAgentRunRequestDto>(json, JsonOptions);
        Assert.NotNull(back);
        Assert.Equal(original.AgentName, back.AgentName);
        Assert.Equal(original.Objective, back.Objective);
        Assert.Equal(original.TraceId, back.TraceId);
        Assert.Equal(original.TenantId, back.TenantId);
    }

    [Fact]
    public void ApiErrorDto_round_trips_json()
    {
        var original = new ApiErrorDto("Code", "Message", "trace");
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var back = JsonSerializer.Deserialize<ApiErrorDto>(json, JsonOptions);
        Assert.NotNull(back);
        Assert.Equal(original.Error, back.Error);
        Assert.Equal(original.Message, back.Message);
        Assert.Equal(original.TraceId, back.TraceId);
    }

    [Fact]
    public void AgentRunSummaryDto_round_trips_json()
    {
        var id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var original = new AgentRunSummaryDto(
            id,
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            null,
            null,
            null,
            null,
            "Agent",
            "trace",
            AgentRunStatus.Completed,
            DateTimeOffset.Parse("2026-01-02T03:04:05Z"),
            DateTimeOffset.Parse("2026-01-02T03:05:06Z"));
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var back = JsonSerializer.Deserialize<AgentRunSummaryDto>(json, JsonOptions);
        Assert.NotNull(back);
        Assert.Equal(original.Id, back.Id);
        Assert.Equal(original.Status, back.Status);
    }
}