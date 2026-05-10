using System.Diagnostics.CodeAnalysis;
using Agentor.Domain;

namespace Agentor.Application.Abstractions;

/// <summary>
/// Catalog that never resolves a skill (default wiring when no skills are registered).
/// </summary>
public sealed class EmptySkillPackageCatalog : ISkillPackageCatalog
{
    public bool TryGet(string skillKey, AgentRecipeVersion version, [NotNullWhen(true)] out SkillPackage? package)
    {
        _ = skillKey;
        _ = version;
        package = null;
        return false;
    }

    public IReadOnlyList<SkillPackage> ListRegisteredPackages() => [];

    public void RegisterPackage(SkillPackage package) =>
        throw new NotSupportedException("Skill registration is not available for EmptySkillPackageCatalog.");
}
