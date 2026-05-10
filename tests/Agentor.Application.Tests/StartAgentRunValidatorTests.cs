using Agentor.Application.Commands;
using Agentor.Application.Validation;

namespace Agentor.Application.Tests;

public sealed class StartAgentRunValidatorTests
{
    [Fact]
    public void Validate_ValidCommand_ReturnsOk()
    {
        var command = new StartAgentRunCommand("Agent", "Do something useful.", "trace-1");

        var result = StartAgentRunValidator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_EmptyObjective_ReturnsFailWithError()
    {
        var command = new StartAgentRunCommand("Agent", "", null);

        var result = StartAgentRunValidator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Objective"));
    }

    [Fact]
    public void Validate_WhitespaceObjective_ReturnsFailWithError()
    {
        var command = new StartAgentRunCommand("Agent", "   ", null);

        var result = StartAgentRunValidator.Validate(command);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Validate_ObjectiveTooLong_ReturnsFailWithError()
    {
        var longObjective = new string('x', StartAgentRunValidator.MaxObjectiveLength + 1);
        var command = new StartAgentRunCommand("Agent", longObjective, null);

        var result = StartAgentRunValidator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains(StartAgentRunValidator.MaxObjectiveLength.ToString()));
    }

    [Fact]
    public void Validate_ObjectiveAtMaxLength_ReturnsOk()
    {
        var maxObjective = new string('x', StartAgentRunValidator.MaxObjectiveLength);
        var command = new StartAgentRunCommand("Agent", maxObjective, null);

        var result = StartAgentRunValidator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_TraceIdTooLong_ReturnsFailWithError()
    {
        var longTraceId = new string('t', StartAgentRunValidator.MaxTraceIdLength + 1);
        var command = new StartAgentRunCommand("Agent", "Valid objective.", longTraceId);

        var result = StartAgentRunValidator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("TraceId"));
    }

    [Fact]
    public void Validate_NullTraceId_ReturnsOk()
    {
        var command = new StartAgentRunCommand("Agent", "Valid objective.", null);

        var result = StartAgentRunValidator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_MultipleErrors_ReturnsAllErrors()
    {
        var longObjective = new string('x', StartAgentRunValidator.MaxObjectiveLength + 1);
        var longTraceId = new string('t', StartAgentRunValidator.MaxTraceIdLength + 1);
        var command = new StartAgentRunCommand("Agent", longObjective, longTraceId);

        var result = StartAgentRunValidator.Validate(command);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 2);
    }
}
