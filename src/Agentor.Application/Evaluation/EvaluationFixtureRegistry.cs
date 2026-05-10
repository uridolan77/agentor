using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agentor.Application.Evaluation;

public sealed record EvaluationFixtureEntry(string Id, string FileName, string? Description);

/// <summary>
/// Versioned index of evaluation JSON fixtures (Phase 14 / PR66).
/// </summary>
public sealed class EvaluationFixtureRegistry
{
    public const int RegistrySchemaVersion = 4;
    public const string RegistryKind = "EvaluationFixtureRegistry";

    private readonly string _fixtureDirectory;
    private readonly IReadOnlyList<EvaluationFixtureEntry> _entries;

    private EvaluationFixtureRegistry(string fixtureDirectory, IReadOnlyList<EvaluationFixtureEntry> entries)
    {
        _fixtureDirectory = fixtureDirectory;
        _entries = entries;
    }

    public IReadOnlyList<EvaluationFixtureEntry> Entries => _entries;

    public static EvaluationFixtureRegistry Load(string registryJsonPath, string fixtureDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registryJsonPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fixtureDirectory);

        if (!File.Exists(registryJsonPath))
        {
            throw new FileNotFoundException("Evaluation fixture registry file not found.", registryJsonPath);
        }

        if (!Directory.Exists(fixtureDirectory))
        {
            throw new DirectoryNotFoundException($"Fixture directory not found: {fixtureDirectory}");
        }

        var json = File.ReadAllText(registryJsonPath);
        var dto = JsonSerializer.Deserialize<RegistryDto>(json, RegistryJsonOptions)
                  ?? throw new InvalidDataException("Registry JSON deserialized to null.");

        if (dto.SchemaVersion != RegistrySchemaVersion)
        {
            throw new InvalidDataException($"Registry schemaVersion must be {RegistrySchemaVersion}, got {dto.SchemaVersion}.");
        }

        if (!string.Equals(dto.Kind, RegistryKind, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Registry kind must be '{RegistryKind}', got '{dto.Kind}'.");
        }

        var entries = new List<EvaluationFixtureEntry>();
        foreach (var e in dto.Entries ?? [])
        {
            if (string.IsNullOrWhiteSpace(e.Id) || string.IsNullOrWhiteSpace(e.FileName))
            {
                throw new InvalidDataException("Registry entry requires non-empty id and fileName.");
            }

            entries.Add(new EvaluationFixtureEntry(e.Id.Trim(), e.FileName.Trim(), e.Description));
        }

        if (entries.Count == 0)
        {
            throw new InvalidDataException("Registry must contain at least one entry.");
        }

        return new EvaluationFixtureRegistry(fixtureDirectory, entries);
    }

    /// <summary>
    /// Enumerates JSON fixture files under <paramref name="directory"/> (non-recursive), excluding registry.json.
    /// </summary>
    public static IReadOnlyList<string> DiscoverFixtureFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directory}");
        }

        return Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Where(p => !string.Equals(Path.GetFileName(p), "registry.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public HarnessFixtureDefinition LoadHarnessFixture(string entryId)
    {
        var entry = _entries.FirstOrDefault(e => string.Equals(e.Id, entryId, StringComparison.OrdinalIgnoreCase))
                    ?? throw new KeyNotFoundException($"Unknown registry entry id '{entryId}'.");

        var path = Path.Combine(_fixtureDirectory, entry.FileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Fixture file for entry '{entryId}' not found.", path);
        }

        return HarnessFixtureJsonParser.Parse(File.ReadAllText(path));
    }

    private sealed class RegistryDto
    {
        public int SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public List<EntryDto>? Entries { get; set; }
    }

    private sealed class EntryDto
    {
        public string? Id { get; set; }
        public string? FileName { get; set; }
        public string? Description { get; set; }
    }

    private static readonly JsonSerializerOptions RegistryJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
