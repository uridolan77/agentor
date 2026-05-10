namespace Agentor.Infrastructure.Options;

/// <summary>Configuration for one integration family under <c>Agentor:Integrations:*</c>.</summary>
public sealed class IntegrationFamilyOptions
{
    public IntegrationAdapterMode Mode { get; set; } = IntegrationAdapterMode.Fake;

    public HttpIntegrationOptions? Http { get; set; }
}
