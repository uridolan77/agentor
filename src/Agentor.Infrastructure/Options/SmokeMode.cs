namespace Agentor.Infrastructure.Options;

/// <summary>
/// Operator integration smoke mode under <c>Agentor:IntegrationSmoke:*</c> (distinct from runtime <see cref="IntegrationAdapterMode"/> naming but maps 1:1 for execution).
/// </summary>
public enum SmokeMode
{
    Disabled = 0,
    Fake = 1,
    Http = 2,
}
