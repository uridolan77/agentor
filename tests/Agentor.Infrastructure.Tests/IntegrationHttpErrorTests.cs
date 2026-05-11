using System.Net;
using System.Net.Http;
using Agentor.Infrastructure.Http;
using Xunit;

namespace Agentor.Infrastructure.Tests;

public sealed class IntegrationHttpErrorTests
{
    [Fact]
    public void RedactAndTruncate_replaces_bearer_prefix()
    {
        var s = IntegrationHttpError.RedactAndTruncate("err: Bearer eyJ0eXAiOiJKV1QiLCJhbGc");
        Assert.DoesNotContain("eyJ", s, StringComparison.Ordinal);
        Assert.Contains("Bearer [REDACTED]", s, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactAndTruncate_replaces_quoted_apiKey_json()
    {
        var s = IntegrationHttpError.RedactAndTruncate("""{"apiKey":"super-secret-key","ok":1}""");
        Assert.DoesNotContain("super-secret", s, StringComparison.Ordinal);
        Assert.Contains("\"apiKey\":\"[REDACTED]\"", s, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RedactAndTruncate_replaces_token_equals_query_style()
    {
        var s = IntegrationHttpError.RedactAndTruncate("detail=token=abc123def&x=1");
        Assert.DoesNotContain("abc123", s, StringComparison.Ordinal);
        Assert.Contains("token=[REDACTED]", s, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RedactAndTruncate_appends_ellipsis_when_over_max()
    {
        var longBody = new string('x', 600);
        var s = IntegrationHttpError.RedactAndTruncate(longBody, maxChars: 100);
        Assert.Equal(101, s.Length);
        Assert.EndsWith("\u2026", s, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThrowIfUnsuccessfulAsync_sets_status_code_on_exception()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent("{\"reason\":\"no\"}"),
        };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            IntegrationHttpError.ThrowIfUnsuccessfulAsync(response, "Test", CancellationToken.None));

        Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
        Assert.Contains("409", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThrowIfUnsuccessfulAsync_appends_correlation_suffix_when_context_set()
    {
        using var _ = Agentor.Application.Observability.AgentorCorrelationContext.Push("abc123correlation");
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"reason\":\"no\"}"),
        };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            IntegrationHttpError.ThrowIfUnsuccessfulAsync(response, "Test", CancellationToken.None));

        Assert.Contains("CorrelationId=abc123correlation", ex.Message, StringComparison.Ordinal);
    }
}
