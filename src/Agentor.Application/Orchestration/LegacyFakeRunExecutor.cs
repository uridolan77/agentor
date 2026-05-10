using Agentor.Application;
using Agentor.Domain;

namespace Agentor.Application.Orchestration;

public sealed class LegacyFakeRunExecutor
{
    private readonly GovernedSingleToolRunDriver _driver;

    public LegacyFakeRunExecutor(GovernedSingleToolRunDriver driver)
    {
        _driver = driver;
    }

    public Task<AgentRun> ExecuteAsync(RunOrchestrationRequest request, CancellationToken cancellationToken) =>
        _driver.ExecuteAsync(
            request,
            profilePurpose: "PR1 deterministic fake agent profile.",
            stepSummary: "Execute deterministic fake tool",
            toolKey: WellKnownToolKeys.Pr1FakeTool,
            cancellationToken);
}
