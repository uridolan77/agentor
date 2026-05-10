using Agentor.Application.Abstractions;
using Agentor.Contracts.KnowledgeState;
using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Athanor;

public sealed class SubmitAthanorCandidateHandler(
    IAgentRunRepository repository,
    IKnowledgeStateClient knowledgeState,
    IClock clock)
{
    public async Task<(bool? Ok, Guid? CandidateId)> HandleAsync(
        Guid runId,
        string summary,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var run = await repository.GetAsync(runId, cancellationToken);
        if (run is null)
        {
            return (null, null);
        }

        if (run.Status != AgentRunStatus.Running)
        {
            return (false, null);
        }

        var submission = new CandidateKnowledgeSubmissionDto(summary, payloadJson);
        var result = await knowledgeState.SubmitCandidateAsync(run.ResolveAthanorProjectId(), run.Id, submission, cancellationToken);
        run.RecordAthanorCandidateSubmission(result.CandidateId, summary, clock.UtcNow);
        await repository.SaveAsync(run, cancellationToken);
        return (true, result.CandidateId);
    }
}