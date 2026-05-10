using Agentor.Application.Commands;

namespace Agentor.Application.Validation;

public static class StartAgentRunValidator
{
    public const int MaxObjectiveLength = 2000;
    public const int MaxTraceIdLength = 128;

    public static ValidationResult Validate(StartAgentRunCommand command)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(command.Objective))
        {
            errors.Add("Objective is required.");
        }
        else if (command.Objective.Length > MaxObjectiveLength)
        {
            errors.Add($"Objective must not exceed {MaxObjectiveLength} characters.");
        }

        if (command.TraceId is not null && command.TraceId.Length > MaxTraceIdLength)
        {
            errors.Add($"TraceId must not exceed {MaxTraceIdLength} characters.");
        }

        return errors.Count == 0 ? ValidationResult.Ok() : ValidationResult.Fail(errors);
    }
}
