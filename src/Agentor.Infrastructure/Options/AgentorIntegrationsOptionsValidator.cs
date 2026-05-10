using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.Options;

public sealed class AgentorIntegrationsOptionsValidator : IValidateOptions<AgentorIntegrationsOptions>
{
    public ValidateOptionsResult Validate(string? name, AgentorIntegrationsOptions options)
    {
        var errors = new List<string>();
        ValidateFamily(nameof(options.Athanor), options.Athanor, errors);
        ValidateFamily(nameof(options.Conexus), options.Conexus, errors);
        ValidateFamily(nameof(options.Mcp), options.Mcp, errors);
        ValidateFamily(nameof(options.ExternalAgents), options.ExternalAgents, errors);
        return errors.Count > 0 ? ValidateOptionsResult.Fail(errors) : ValidateOptionsResult.Success;
    }

    private static void ValidateFamily(string label, IntegrationFamilyOptions family, List<string> errors)
    {
        if (!Enum.IsDefined(family.Mode))
        {
            errors.Add($"{label}.{nameof(IntegrationFamilyOptions.Mode)} has an invalid enum value.");
            return;
        }

        if (family.Mode != IntegrationAdapterMode.Http)
        {
            return;
        }

        if (family.Http is null || string.IsNullOrWhiteSpace(family.Http.BaseUrl))
        {
            errors.Add($"{label}: when Mode is Http, {nameof(HttpIntegrationOptions.BaseUrl)} is required.");
            return;
        }

        if (!Uri.TryCreate(family.Http.BaseUrl.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            errors.Add($"{label}.{nameof(HttpIntegrationOptions.BaseUrl)} must be an absolute http(s) URL.");
        }
    }
}
