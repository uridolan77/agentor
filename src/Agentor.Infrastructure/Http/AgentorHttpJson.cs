using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agentor.Infrastructure.Http;

internal static class AgentorHttpJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
