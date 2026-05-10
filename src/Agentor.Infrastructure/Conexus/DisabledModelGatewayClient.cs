using Agentor.Application.Abstractions;
using Agentor.Contracts.Conexus;

namespace Agentor.Infrastructure.Conexus;

public sealed class DisabledModelGatewayClient : IModelGatewayClient
{
    private const string Message =
        "Conexus integration is disabled (configure Agentor:Integrations:Conexus:Mode).";

    public Task<ModelCallResultDto> CompleteAsync(ModelCallRequestDto request, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(Message);
    }
}
