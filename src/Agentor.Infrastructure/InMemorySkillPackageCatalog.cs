using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Agentor.Application.Abstractions;
using Agentor.Domain;

namespace Agentor.Infrastructure;

/// <summary>
/// In-memory skill catalog for tests and harness runs. Thread-safe.
/// </summary>
public sealed class InMemorySkillPackageCatalog : ISkillPackageCatalog
{
    private readonly ConcurrentDictionary<(string Key, string Version), SkillPackage> _packages = new();

    public void Register(SkillPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        _packages[(package.SkillKey, package.Version.Value)] = package;
    }

    public bool TryGet(string skillKey, AgentRecipeVersion version, [NotNullWhen(true)] out SkillPackage? package)
    {
        return _packages.TryGetValue((skillKey.Trim(), version.Value), out package);
    }

    public IReadOnlyList<SkillPackage> ListRegisteredPackages() =>
        _packages.Values
            .OrderBy(p => p.SkillKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Version.Value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Id)
            .ToList();

    public void RegisterPackage(SkillPackage package) => Register(package);
}
