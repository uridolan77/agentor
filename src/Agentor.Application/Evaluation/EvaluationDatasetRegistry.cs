using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agentor.Application.Evaluation;

/// <summary>
/// Loads JSON evaluation dataset definitions and validates fixture references (PR123).
/// </summary>
public sealed class EvaluationDatasetRegistry
{
    public const int RegistrySchemaVersion = 1;
    public const string RegistryKind = "EvaluationDatasetRegistry";

    private readonly IReadOnlyList<EvaluationDataset> _datasets;

    private EvaluationDatasetRegistry(IReadOnlyList<EvaluationDataset> datasets)
    {
        _datasets = datasets;
    }

    public IReadOnlyList<EvaluationDataset> Datasets => _datasets;

    /// <summary>
    /// Loads registry JSON and validates fixture ids against <paramref name="fixtureRegistry"/>.
    /// Entries are enumerated in JSON array order; callers should sort if they need deterministic iteration beyond file order.
    /// </summary>
    public static EvaluationDatasetRegistry Load(string registryJsonPath, EvaluationFixtureRegistry fixtureRegistry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registryJsonPath);
        ArgumentNullException.ThrowIfNull(fixtureRegistry);

        if (!File.Exists(registryJsonPath))
        {
            throw new FileNotFoundException("Evaluation dataset registry file not found.", registryJsonPath);
        }

        var json = File.ReadAllText(registryJsonPath);
        var dto = JsonSerializer.Deserialize<RegistryDto>(json, RegistryJsonOptions)
                  ?? throw new InvalidDataException("Dataset registry JSON deserialized to null.");

        if (dto.SchemaVersion != RegistrySchemaVersion)
        {
            throw new InvalidDataException($"Dataset registry schemaVersion must be {RegistrySchemaVersion}, got {dto.SchemaVersion}.");
        }

        if (!string.Equals(dto.Kind, RegistryKind, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Dataset registry kind must be '{RegistryKind}', got '{dto.Kind}'.");
        }

        var fixtureIds = new HashSet<string>(fixtureRegistry.Entries.Select(e => e.Id), StringComparer.OrdinalIgnoreCase);

        var datasets = new List<EvaluationDataset>();
        var datasetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var globalCaseIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ds in dto.Datasets ?? [])
        {
            if (string.IsNullOrWhiteSpace(ds.Id))
            {
                throw new InvalidDataException("Each dataset requires a non-empty id.");
            }

            var did = ds.Id.Trim();
            if (!datasetIds.Add(did))
            {
                throw new InvalidDataException($"Duplicate dataset id '{did}'.");
            }

            var cases = new List<EvaluationCase>();
            var seenCaseInDataset = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var c in ds.Cases ?? [])
            {
                if (string.IsNullOrWhiteSpace(c.Id))
                {
                    throw new InvalidDataException($"Dataset '{did}' contains a case with an empty id.");
                }

                var cid = c.Id.Trim();
                if (!seenCaseInDataset.Add(cid))
                {
                    throw new InvalidDataException($"Duplicate case id '{cid}' in dataset '{did}'.");
                }

                if (!globalCaseIds.Add(cid))
                {
                    throw new InvalidDataException($"Duplicate case id '{cid}' across datasets.");
                }

                if (string.IsNullOrWhiteSpace(c.FixtureId))
                {
                    throw new InvalidDataException($"Case '{cid}' in dataset '{did}' requires fixtureId.");
                }

                var fid = c.FixtureId.Trim();
                if (!fixtureIds.Contains(fid))
                {
                    throw new InvalidDataException(
                        $"Case '{cid}' references unknown fixture id '{fid}'. Known fixtures: {string.Join(", ", fixtureIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))}.");
                }

                if (!Enum.TryParse<CoordinationEvaluationProfile>(c.Profile, ignoreCase: true, out var profile))
                {
                    throw new InvalidDataException($"Case '{cid}' has unknown profile '{c.Profile}'.");
                }

                var tags = new List<EvaluationCaseTag>();
                foreach (var t in c.Tags ?? [])
                {
                    if (!TryParseTag(t, out var tag))
                    {
                        throw new InvalidDataException($"Case '{cid}' has unknown tag '{t}'.");
                    }

                    tags.Add(tag);
                }

                cases.Add(new EvaluationCase(cid, fid, profile, tags));
            }

            if (cases.Count == 0)
            {
                throw new InvalidDataException($"Dataset '{did}' must contain at least one case.");
            }

            datasets.Add(new EvaluationDataset(did, cases));
        }

        if (datasets.Count == 0)
        {
            throw new InvalidDataException("Dataset registry must contain at least one dataset.");
        }

        return new EvaluationDatasetRegistry(datasets);
    }

    /// <summary>
    /// Returns cases in all datasets that include every tag in <paramref name="requiredTags"/> (AND semantics).
    /// When <paramref name="requiredTags"/> is null or empty, returns every case in stable order (dataset id, then case id).
    /// </summary>
    public IReadOnlyList<EvaluationCase> SelectCases(IReadOnlySet<EvaluationCaseTag>? requiredTags = null)
    {
        var q = Datasets
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .SelectMany(d => d.Cases.OrderBy(c => c.Id, StringComparer.Ordinal))
            .Where(c => CaseMatchesTags(c, requiredTags))
            .ToList();
        return q;
    }

    public static bool CaseMatchesTags(EvaluationCase c, IReadOnlySet<EvaluationCaseTag>? requiredTags)
    {
        if (requiredTags is null || requiredTags.Count == 0)
        {
            return true;
        }

        return requiredTags.All(t => c.Tags.Contains(t));
    }

    internal static bool TryParseTag(string raw, out EvaluationCaseTag tag)
    {
        tag = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var s = raw.Trim();
        if (Enum.TryParse<EvaluationCaseTag>(s, ignoreCase: true, out tag))
        {
            return true;
        }

        if (string.Equals(s, "external-agent", StringComparison.OrdinalIgnoreCase))
        {
            tag = EvaluationCaseTag.ExternalAgent;
            return true;
        }

        return false;
    }

    private sealed class RegistryDto
    {
        public int SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public List<DatasetDto>? Datasets { get; set; }
    }

    private sealed class DatasetDto
    {
        public string? Id { get; set; }
        public List<CaseDto>? Cases { get; set; }
    }

    private sealed class CaseDto
    {
        public string? Id { get; set; }
        public string? FixtureId { get; set; }
        public string? Profile { get; set; }
        public List<string>? Tags { get; set; }
    }

    private static readonly JsonSerializerOptions RegistryJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
