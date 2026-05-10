using Agentor.Application.Options;
using Agentor.Application.Redaction;
using Xunit;

namespace Agentor.Application.Tests.Redaction;

public sealed class RedactionPolicyTests
{
    [Fact]
    public void CatalogDefault_contains_common_sensitive_substrings()
    {
        var policy = RedactionPolicy.CatalogDefault;
        Assert.Contains("apiKey", policy.KeyNameSubstrings, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("[REDACTED]", policy.ReplacementToken);
    }

    [Fact]
    public void FromAuditExportOptions_throws_on_null_options()
    {
        Assert.Throws<ArgumentNullException>(() => RedactionPolicy.FromAuditExportOptions(null!));
    }

    [Fact]
    public void ctor_uses_custom_replacement_token()
    {
        var policy = new RedactionPolicy(["token"], "***");
        Assert.Equal("***", policy.ReplacementToken);
    }
}