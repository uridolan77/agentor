namespace Agentor.Domain.Enums;

public enum RecipeStepKind
{
    Tool,

    /// <summary>Invokes a versioned <see cref="SkillPackage"/> procedure (still governed per inner tool call).</summary>
    Skill
}
