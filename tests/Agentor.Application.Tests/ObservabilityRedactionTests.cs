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

    [Fact]
    public void SanitizeForLog_redacts_nested_json_secret_keys_in_flattened_text()
    {
        var raw = """upstream: {"apiKey":"sk-1234567890","nested":{"password":"hunter2"}}""";
        var s = ObservabilityRedaction.SanitizeForLog(raw);
        Assert.DoesNotContain("sk-1234567890", s, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", s, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", s, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizeExceptionMessage_redacts_token_style_pairs()
    {
        var ex = new InvalidOperationException("token=super-secret-value");
        var s = ObservabilityRedaction.SanitizeExceptionMessage(ex);
        Assert.DoesNotContain("super-secret", s, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", s, StringComparison.Ordinal);
    }
}
