using Agentor.Application.Abstractions;
using Agentor.Contracts.Conexus;

namespace Agentor.Infrastructure.Conexus;

public sealed class FakeModelGatewayClient : IModelGatewayClient
{
    public const string FakeProviderName = "fake-conexus";

    public Task<ModelCallResultDto> CompleteAsync(ModelCallRequestDto request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var modelId = string.IsNullOrWhiteSpace(request.ModelId) ? "fake-model" : request.ModelId.Trim();
        var prompt = request.Prompt ?? string.Empty;

        var promptTokens = EstimateTokens(prompt);
        var completionTokens = Math.Max(1, promptTokens / 8 + 3);
        var latencyMs = Math.Max(1, (prompt.Length % 97) + 5);

        var preview = prompt.Length <= 120 ? prompt : prompt[..120];
        var completion =
            "[" + FakeProviderName + ":" + modelId + "] " +
            (prompt.Length == 0 ? "(empty prompt)" : ("echo:" + preview));

        var costUnits = decimal.Round(
            (promptTokens + completionTokens) * 0.001m,
            6,
            MidpointRounding.AwayFromZero);

        return Task.FromResult(
            new ModelCallResultDto(
                CompletionText: completion,
                ProviderName: FakeProviderName,
                ModelId: modelId,
                PromptTokens: promptTokens,
                CompletionTokens: completionTokens,
                EstimatedCostUnits: costUnits,
                LatencyMs: latencyMs,
                PromptProfileRef: request.PromptProfileRef,
                ModelProfileRef: request.ModelProfileRef));
    }

    private static int EstimateTokens(string text)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var approx = (int)Math.Ceiling(text.Length / 4.0);
        return Math.Max(1, approx);
    }
}