using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Agentor.Api.Security;

public sealed class AgentorAuthOptionsValidator(IHostEnvironment hostEnvironment)
    : IValidateOptions<AgentorAuthOptions>
{
    public ValidateOptionsResult Validate(string? name, AgentorAuthOptions options)
    {
        if (options.Mode == AgentorAuthMode.Fake
            && !options.AllowFakeOutsideDevelopment
            && !IsLocalOrTestEnvironment(hostEnvironment))
        {
            return ValidateOptionsResult.Fail(
                "Agentor:Auth:Mode=Fake is only allowed in Development/Test by default. " +
                "Set Agentor:Auth:AllowFakeOutsideDevelopment=true to override explicitly.");
        }

        if (options.Mode == AgentorAuthMode.Header
            && string.IsNullOrWhiteSpace(options.HeaderActorIdHeaderName))
        {
            return ValidateOptionsResult.Fail(
                "Agentor:Auth:HeaderActorIdHeaderName is required when Agentor:Auth:Mode=Header.");
        }

        if (options.Mode == AgentorAuthMode.Jwt)
        {
            if (options.JwtActorIdClaimTypes.Count == 0)
            {
                return ValidateOptionsResult.Fail(
                    "Agentor:Auth:JwtActorIdClaimTypes must include at least one claim type when Agentor:Auth:Mode=Jwt.");
            }

            if (string.IsNullOrWhiteSpace(options.JwtRoleClaimType))
            {
                return ValidateOptionsResult.Fail(
                    "Agentor:Auth:JwtRoleClaimType is required when Agentor:Auth:Mode=Jwt.");
            }
        }

        return ValidateOptionsResult.Success;
    }

    private static bool IsLocalOrTestEnvironment(IHostEnvironment environment)
        => environment.IsDevelopment()
           || environment.IsEnvironment("Test")
           || environment.IsEnvironment("Testing");
}
