using System.Diagnostics.CodeAnalysis;
using Agentor.Domain;

namespace Agentor.Application.Abstractions;

/// <summary>
/// Resolves versioned <see cref="SkillPackage"/> definitions for coordination. No execution behavior.
/// </summary>
public interface ISkillPackageCatalog
{
    bool TryGet(string skillKey, AgentRecipeVersion version, [NotNullWhen(true)] out SkillPackage? package);
}
