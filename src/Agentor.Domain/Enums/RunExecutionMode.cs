namespace Agentor.Domain.Enums;

/// <summary>
/// How <see cref="Agentor.Application.Orchestration.RunOrchestrationRequest"/> should be executed at run start.
/// </summary>
public enum RunExecutionMode
{
    LegacyFakeTool,
    SingleTool,
    Plan,
    Recipe,
    Skill,
    ModelCall,
    McpTool,
    ExternalAgent
}
