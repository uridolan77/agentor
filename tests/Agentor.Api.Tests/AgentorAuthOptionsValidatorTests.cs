using Agentor.Api.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Agentor.Api.Tests;

public sealed class AgentorAuthOptionsValidatorTests
{
    [Fact]
    public void Validate_Fails_WhenFakeModeUsedInProductionByDefault()
    {
        var validator = new AgentorAuthOptionsValidator(new FakeHostEnvironment("Production"));
        var options = new AgentorAuthOptions
        {
            Mode = AgentorAuthMode.Fake,
            AllowFakeOutsideDevelopment = false
        };

        var result = validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains("only allowed in Development/Test", result.FailureMessage);
    }

    [Fact]
    public void Validate_Succeeds_WhenFakeModeUsedInDevelopment()
    {
        var validator = new AgentorAuthOptionsValidator(new FakeHostEnvironment(Environments.Development));
        var options = new AgentorAuthOptions
        {
            Mode = AgentorAuthMode.Fake,
            AllowFakeOutsideDevelopment = false
        };

        var result = validator.Validate(Options.DefaultName, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_Fails_WhenHeaderModeHasNoHeaderName()
    {
        var validator = new AgentorAuthOptionsValidator(new FakeHostEnvironment(Environments.Development));
        var options = new AgentorAuthOptions
        {
            Mode = AgentorAuthMode.Header,
            HeaderActorIdHeaderName = " "
        };

        var result = validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains("HeaderActorIdHeaderName", result.FailureMessage);
    }

    [Fact]
    public void Validate_Fails_WhenJwtModeHasNoActorIdClaimTypes()
    {
        var validator = new AgentorAuthOptionsValidator(new FakeHostEnvironment(Environments.Development));
        var options = new AgentorAuthOptions
        {
            Mode = AgentorAuthMode.Jwt,
            JwtActorIdClaimTypes = []
        };

        var result = validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains("JwtActorIdClaimTypes", result.FailureMessage);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Agentor.Api.Tests";

        public string ContentRootPath { get; set; } = ".";

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
