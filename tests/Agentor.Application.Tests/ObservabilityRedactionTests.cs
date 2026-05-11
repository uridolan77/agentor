using Agentor.Application.Observability;

namespace Agentor.Application.Tests;

public sealed class ObservabilityRedactionTests
{
    [Fact]
    public void SanitizeForLog_strips_bearer_and_truncates()
    {
        var s = ObservabilityRedaction.SanitizeForLog("Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.body");
        Assert.DoesNotContain("eyJ", s, StringComparison.Ordinal);
        Assert.Contains("Bearer [REDACTED]", s, StringComparison.Ordinal);
    }
}
