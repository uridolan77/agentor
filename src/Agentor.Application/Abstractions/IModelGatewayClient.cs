using Agentor.Contracts.Conexus;

namespace Agentor.Application.Abstractions;

/// <summary>
/// Application port for Conexus (model routing). Implementations live in Infrastructure adapters.
/// </summary>
public interface IModelGatewayClient
{
    Task<ModelCallResultDto> CompleteAsync(ModelCallRequestDto request, CancellationToken cancellationToken);
}
