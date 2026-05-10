using System.ComponentModel.DataAnnotations;

namespace Agentor.Infrastructure.Options;

public sealed class AgentorPersistenceOptions
{
    public const string SectionName = "AgentorPersistence";

    public const string ModeInMemory = "InMemory";
    public const string ModePostgres = "Postgres";

    [Required, MinLength(1)]
    public string Mode { get; set; } = ModeInMemory;

    public string? ConnectionString { get; set; }
}
