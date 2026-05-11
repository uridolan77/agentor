namespace Agentor.Application.Observability;

/// <summary>
/// Async request correlation id (typically the same value as <c>X-Agentor-Trace-Id</c>) for logs and integration HTTP.
/// </summary>
public static class AgentorCorrelationContext
{
    private static readonly AsyncLocal<string?> CurrentValue = new();

    public static string? Current => CurrentValue.Value;

    public static IDisposable Push(string traceId)
    {
        var prior = CurrentValue.Value;
        CurrentValue.Value = traceId;
        return new PopScope(prior);
    }

    private sealed class PopScope : IDisposable
    {
        private readonly string? _prior;
        private bool _disposed;

        public PopScope(string? prior) => _prior = prior;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CurrentValue.Value = _prior;
        }
    }
}
