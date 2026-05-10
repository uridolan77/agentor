using System.ComponentModel.DataAnnotations;

namespace Agentor.Application.Options;

public sealed class AgentorRuntimeOptions
{
    public const string SectionName = "AgentorRuntime";

    [Required, MinLength(1), MaxLength(200)]
    public string ServiceName { get; set; } = "Agentor.Api";

    [Required, MinLength(1), MaxLength(50)]
    public string Version { get; set; } = "0.1.0";
}
