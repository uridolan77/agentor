using Agentor.Application.Commands;

namespace Agentor.Application.RunQueue;

public sealed record RunWorkItem(Guid WorkItemId, StartAgentRunCommand Command);
