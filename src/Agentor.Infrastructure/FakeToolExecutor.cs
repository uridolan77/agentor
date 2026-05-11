using Agentor.Application.Abstractions;
using Agentor.Domain;

namespace Agentor.Infrastructure;

public sealed class FakeToolExecutor : IToolExecutor
{
    public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken)
    {
        var flat = request.Input.ToPolicyEvaluationDictionary();
        var objective = flat.TryGetValue("objective", out var value)
            ? value
            : "No objective provided.";

        var output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["message"] = "PR1 fake tool executed successfully.",
            ["objectiveEcho"] = objective,
            ["toolKey"] = request.ToolKey
        };

        return Task.FromResult(new ToolExecutionResult(true, ToolPayload.FromLegacyDictionary(output)));
    }
}
