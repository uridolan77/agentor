using Agentor.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Agentor.Api.Tests.Support;

/// <summary>
/// Uses TestAgentRunRepository so tests can seed an AgentRun that remains Running.
/// </summary>
public sealed class AthanorRunningRunApiFixture : WebApplicationFactory<Program>
{
    public TestAgentRunRepository Repository { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAgentRunRepository>();
            services.AddSingleton<IAgentRunRepository>(Repository);
        });
    }
}