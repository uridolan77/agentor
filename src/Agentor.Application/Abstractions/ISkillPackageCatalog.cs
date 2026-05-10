using System.Diagnostics.CodeAnalysis;
using Agentor.Domain;

namespace Agentor.Application.Abstractions;

/// <summary>
/// Resolves versioned <see cref="SkillPackage"/> definitions for coordination. No execution behavior.
/// </summary>
public interface ISkillPackageCatalog
{
    bool TryGet(string skillKey, AgentRecipeVersion version, [NotNullWhen(true)] out SkillPackage? package);

    /// <summary>Registered packages for operator listing (deterministic order: skill key, version, id).</summary>
    IReadOnlyList<SkillPackage> ListRegisteredPackages();

    /// <summary>Registers a validated package; duplicates overwrite the same (skillKey, version) slot.</summary>
    void RegisterPackage(SkillPackage package);
}
