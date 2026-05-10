using Agentor.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.Tests;

public sealed class IntegrationOptionsValidatorTests
{
    [Fact]
    public void Validate_HttpModeWithoutBaseUrl_Fails()
    {
        var validator = new AgentorIntegrationsOptionsValidator();
        var options = new AgentorIntegrationsOptions
        {
            Athanor = new IntegrationFamilyOptions { Mode = IntegrationAdapterMode.Http },
        };

        var result = validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.NotNull(result.FailureMessage);
        Assert.True(result.FailureMessage!.Contains("BaseUrl", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_DefaultFakeModes_Succeeds()
    {
        var validator = new AgentorIntegrationsOptionsValidator();
        var options = new AgentorIntegrationsOptions();

        var result = validator.Validate(null, options);

        Assert.False(result.Failed);
    }
}
