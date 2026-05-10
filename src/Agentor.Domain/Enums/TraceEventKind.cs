namespace Agentor.Domain.Enums;

public enum TraceEventKind
{
    RunStarted,
    StepStarted,
    PolicyEvaluated,
    ToolCallStarted,
    ToolCallCompleted,
    StepCompleted,
    RunCompleted,
    RunFailed
}
