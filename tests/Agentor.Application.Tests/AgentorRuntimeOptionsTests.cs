using System.ComponentModel.DataAnnotations;
using Agentor.Application.Options;
using Agentor.Infrastructure.Options;

namespace Agentor.Application.Tests;

public sealed class AgentorRuntimeOptionsTests
{
    [Fact]
    public void AgentorRuntimeOptions_Defaults_PassValidation()
    {
        var opts = new AgentorRuntimeOptions();
        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(opts, new ValidationContext(opts), results, validateAllProperties: true);

        Assert.True(valid);
        Assert.Empty(results);
    }

    [Fact]
    public void AgentorRuntimeOptions_DefaultServiceName_IsAgentorApi()
    {
        Assert.Equal("Agentor.Api", new AgentorRuntimeOptions().ServiceName);
    }

    [Fact]
    public void AgentorRuntimeOptions_DefaultVersion_Is_1_0_0_rc_1()
    {
        Assert.Equal("1.0.0-rc.1", new AgentorRuntimeOptions().Version);
    }

    [Fact]
    public void AgentorRuntimeOptions_EmptyServiceName_FailsValidation()
    {
        var opts = new AgentorRuntimeOptions { ServiceName = "" };
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(opts, new ValidationContext(opts), results, validateAllProperties: true);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(AgentorRuntimeOptions.ServiceName)));
    }

    [Fact]
    public void AgentorPersistenceOptions_Defaults_PassValidation()
    {
        var opts = new AgentorPersistenceOptions();
        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(opts, new ValidationContext(opts), results, validateAllProperties: true);

        Assert.True(valid);
        Assert.Empty(results);
    }

    [Fact]
    public void AgentorPersistenceOptions_DefaultMode_IsInMemory()
    {
        Assert.Equal(AgentorPersistenceOptions.ModeInMemory, new AgentorPersistenceOptions().Mode);
    }

    [Fact]
    public void AgentorPersistenceOptions_EmptyMode_FailsValidation()
    {
        var opts = new AgentorPersistenceOptions { Mode = "" };
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(opts, new ValidationContext(opts), results, validateAllProperties: true);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(AgentorPersistenceOptions.Mode)));
    }

    [Fact]
    public void AgentorPersistenceOptions_SectionName_IsAgentorPersistence()
    {
        Assert.Equal("AgentorPersistence", AgentorPersistenceOptions.SectionName);
    }
}
