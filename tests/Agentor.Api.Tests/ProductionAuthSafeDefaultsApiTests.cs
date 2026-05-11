using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Agentor.Api.Tests;

/// <summary>
/// Phase 38 / PR157 — production defaults for auth options (Fake blocked without override).
/// </summary>
public sealed class ProductionAuthSafeDefaultsApiTests
{
    [Fact]
    public void Production_with_default_fake_auth_fails_options_validation_on_host_build()
    {
        var ex = Record.Exception(() =>
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            {
                b.UseEnvironment(Environments.Production);
                b.ConfigureAppConfiguration((_, cfg) =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Agentor:Auth:Mode"] = "Fake",
                        ["Agentor:Auth:AllowFakeOutsideDevelopment"] = "false",
                    });
                });
            });

            using var _ = factory.CreateClient();
        });

        Assert.NotNull(ex);
        Assert.True(
            ex is OptionsValidationException
                || ex.InnerException is OptionsValidationException
                || ex.Message.Contains("Agentor:Auth", StringComparison.OrdinalIgnoreCase),
            $"Unexpected exception: {ex}");
    }
}
