using Agentor.Application.Athanor;
using Agentor.Application.Commands;
using Agentor.Application.HumanReview;
using Agentor.Application.Queries;
using Agentor.Application.Reliability;
using Agentor.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Agentor.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAgentorApplication(this IServiceCollection services)
    {
        services.AddScoped<StartAgentRunHandler>();
        services.AddScoped<AgentRunIdempotencyService>();
        services.AddScoped<GetAgentRunQueryHandler>();
        services.AddScoped<GetRunManifestQueryHandler>();
        services.AddScoped<ListAgentRunsQueryHandler>();
        services.AddScoped<GetAgentRunTraceQueryHandler>();
        services.AddScoped<GetAgentRunStepsQueryHandler>();
        services.AddScoped<GetAgentRunToolCallsQueryHandler>();

        services.AddScoped<ReviewTraceWriter>();
        services.AddScoped<ReviewPolicyReevaluationService>();
        services.AddScoped<PlanResumeOrchestrator>();
        services.AddScoped<ReviewedToolContinuationService>();
        services.AddScoped<HumanReviewDecisionApplicator>();
        services.AddScoped<ApplyHumanReviewDecisionHandler>();
        services.AddScoped<GetRunAuditExportQueryHandler>();

        services.AddScoped<GetRunTimelineQueryHandler>();
        services.AddScoped<GetRunCoordinationViewQueryHandler>();
        services.AddScoped<ListPendingHumanReviewsQueryHandler>();
        services.AddScoped<OperatorDashboardQueryHandler>();

        services.AddScoped<OutboxDispatcher>();

        services.AddScoped<GetLatestAthanorSnapshotForRunQueryHandler>();
        services.AddScoped<LookupAthanorCanonicalForRunQueryHandler>();
        services.AddScoped<AttachAthanorEvidenceProvenanceHandler>();
        services.AddScoped<SubmitAthanorCandidateHandler>();
        services.AddScoped<QueueAthanorReviewHandler>();

        return services;
    }
}
