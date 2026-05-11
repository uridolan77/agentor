using Agentor.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Agentor.Infrastructure.Persistence;

public sealed class AgentorDbContext : DbContext
{
    public AgentorDbContext(DbContextOptions<AgentorDbContext> options) : base(options)
    {
    }

    public DbSet<AgentRunRecord> AgentRuns => Set<AgentRunRecord>();
    public DbSet<AgentStepRecord> AgentSteps => Set<AgentStepRecord>();
    public DbSet<ToolCallRecord> ToolCalls => Set<ToolCallRecord>();
    public DbSet<PolicyDecisionRecord> PolicyDecisions => Set<PolicyDecisionRecord>();
    public DbSet<TraceEventRecord> TraceEvents => Set<TraceEventRecord>();
    public DbSet<AgentRunIdempotencyRecord> AgentRunIdempotencyKeys => Set<AgentRunIdempotencyRecord>();

    public DbSet<OutboxMessageRecord> OutboxMessages => Set<OutboxMessageRecord>();

    public DbSet<RunQueueItemRecord> RunQueueItems => Set<RunQueueItemRecord>();

    public DbSet<ExecutionLeaseRecord> ExecutionLeases => Set<ExecutionLeaseRecord>();

    public DbSet<DistributedOperationRecord> DistributedOperations => Set<DistributedOperationRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AgentRunRecord>(entity =>
        {
            entity.ToTable("agent_runs");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id).HasColumnName("id");
            entity.Property(r => r.ProfileId).HasColumnName("profile_id");
            entity.Property(r => r.TenantId).HasColumnName("tenant_id");
            entity.Property(r => r.WorkspaceId).HasColumnName("workspace_id");
            entity.Property(r => r.ProjectId).HasColumnName("project_id");
            entity.Property(r => r.KnowledgeScopeId).HasColumnName("knowledge_scope_id");
            entity.Property(r => r.AgentName).HasColumnName("agent_name").IsRequired().HasMaxLength(500);
            entity.Property(r => r.Objective).HasColumnName("objective").IsRequired().HasMaxLength(2000);
            entity.Property(r => r.TraceId).HasColumnName("trace_id").IsRequired().HasMaxLength(128);
            entity.Property(r => r.Status).HasColumnName("status").IsRequired().HasMaxLength(50);
            entity.Property(r => r.StartedAt).HasColumnName("started_at");
            entity.Property(r => r.CompletedAt).HasColumnName("completed_at");
            entity.Property(r => r.TerminalAt).HasColumnName("terminal_at");
            entity.Property(r => r.ReviewRequestedAt).HasColumnName("review_requested_at");
            entity.Property(r => r.PausedAt).HasColumnName("paused_at");
            entity.Property(r => r.ReviewWorkflowStatus)
                .HasColumnName("review_workflow_status")
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(r => r.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
            entity.Property(r => r.SessionMemoryJson).HasColumnName("session_memory_json").IsRequired();
            entity.Property(r => r.HumanReviewDecisionsJson).HasColumnName("human_review_decisions_json").IsRequired();
            entity.Property(r => r.ResumeCursorJson).HasColumnName("resume_cursor_json").HasColumnType("text");
            entity.Property(r => r.AggregateVersion)
                .HasColumnName("aggregate_version")
                .IsConcurrencyToken();

            entity.HasMany(r => r.Steps)
                .WithOne(s => s.Run)
                .HasForeignKey(s => s.RunId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(r => r.TraceEvents)
                .WithOne(e => e.Run)
                .HasForeignKey(e => e.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgentStepRecord>(entity =>
        {
            entity.ToTable("agent_steps");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Id).HasColumnName("id");
            entity.Property(s => s.RunId).HasColumnName("run_id");
            entity.Property(s => s.Index).HasColumnName("index");
            entity.Property(s => s.Name).HasColumnName("name").IsRequired().HasMaxLength(500);
            entity.Property(s => s.Status).HasColumnName("status").IsRequired().HasMaxLength(50);
            entity.Property(s => s.StartedAt).HasColumnName("started_at");
            entity.Property(s => s.CompletedAt).HasColumnName("completed_at");

            entity.HasMany(s => s.ToolCalls)
                .WithOne(t => t.Step)
                .HasForeignKey(t => t.StepId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(s => s.PolicyDecisions)
                .WithOne(p => p.Step)
                .HasForeignKey(p => p.StepId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ToolCallRecord>(entity =>
        {
            entity.ToTable("tool_calls");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Id).HasColumnName("id");
            entity.Property(t => t.RunId).HasColumnName("run_id");
            entity.Property(t => t.StepId).HasColumnName("step_id");
            entity.Property(t => t.ToolKey).HasColumnName("tool_key").IsRequired().HasMaxLength(200);
            entity.Property(t => t.Status).HasColumnName("status").IsRequired().HasMaxLength(50);
            entity.Property(t => t.InputJson).HasColumnName("input_json").IsRequired();
            entity.Property(t => t.OutputJson).HasColumnName("output_json").IsRequired();
            entity.Property(t => t.StartedAt).HasColumnName("started_at");
            entity.Property(t => t.CompletedAt).HasColumnName("completed_at");
            entity.Property(t => t.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        });

        modelBuilder.Entity<PolicyDecisionRecord>(entity =>
        {
            entity.ToTable("policy_decisions");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Id).HasColumnName("id");
            entity.Property(p => p.RunId).HasColumnName("run_id");
            entity.Property(p => p.StepId).HasColumnName("step_id");
            entity.Property(p => p.Outcome).HasColumnName("outcome").IsRequired().HasMaxLength(50);
            entity.Property(p => p.ReasonCode).HasColumnName("reason_code").IsRequired().HasMaxLength(200);
            entity.Property(p => p.Reason).HasColumnName("reason").IsRequired().HasMaxLength(2000);
            entity.Property(p => p.DecidedAt).HasColumnName("decided_at");
        });

        modelBuilder.Entity<TraceEventRecord>(entity =>
        {
            entity.ToTable("trace_events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.RunId).HasColumnName("run_id");
            entity.Property(e => e.Kind).HasColumnName("kind").IsRequired().HasMaxLength(100);
            entity.Property(e => e.Message).HasColumnName("message").IsRequired().HasMaxLength(2000);
            entity.Property(e => e.OccurredAt).HasColumnName("occurred_at");
            entity.Property(e => e.DataJson).HasColumnName("data_json").IsRequired();
        });

        modelBuilder.Entity<AgentRunIdempotencyRecord>(entity =>
        {
            entity.ToTable("agent_run_idempotency_keys");
            entity.HasKey(r => r.IdempotencyKey);
            entity.Property(r => r.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(256);
            entity.Property(r => r.RequestFingerprint).HasColumnName("request_fingerprint").IsRequired().HasMaxLength(128);
            entity.Property(r => r.AgentRunId).HasColumnName("agent_run_id");
            entity.Property(r => r.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<OutboxMessageRecord>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id).HasColumnName("id");
            entity.Property(r => r.Kind).HasColumnName("kind").IsRequired().HasMaxLength(64);
            entity.Property(r => r.PayloadJson).HasColumnName("payload_json").IsRequired();
            entity.Property(r => r.Status).HasColumnName("status").IsRequired().HasMaxLength(64);
            entity.Property(r => r.AttemptCount).HasColumnName("attempt_count");
            entity.Property(r => r.CreatedAt).HasColumnName("created_at");
            entity.Property(r => r.LastError).HasColumnName("last_error").HasMaxLength(2000);
        });

        modelBuilder.Entity<RunQueueItemRecord>(entity =>
        {
            entity.ToTable("run_queue_items");
            entity.HasKey(r => r.WorkItemId);
            entity.Property(r => r.WorkItemId).HasColumnName("work_item_id");
            entity.Property(r => r.AgentName).HasColumnName("agent_name").IsRequired().HasMaxLength(500);
            entity.Property(r => r.Objective).HasColumnName("objective").IsRequired().HasMaxLength(2000);
            entity.Property(r => r.TraceId).HasColumnName("trace_id").HasMaxLength(128);
            entity.Property(r => r.TenantId).HasColumnName("tenant_id");
            entity.Property(r => r.WorkspaceId).HasColumnName("workspace_id");
            entity.Property(r => r.ProjectId).HasColumnName("project_id");
            entity.Property(r => r.KnowledgeScopeId).HasColumnName("knowledge_scope_id");
            entity.Property(r => r.Status).HasColumnName("status").IsRequired().HasMaxLength(64);
            entity.Property(r => r.EnqueuedAtUtc).HasColumnName("enqueued_at_utc");
            entity.Property(r => r.ClaimedBy).HasColumnName("claimed_by").HasMaxLength(256);
            entity.Property(r => r.LeaseExpiresAtUtc).HasColumnName("lease_expires_at_utc");
            entity.Property(r => r.AgentRunId).HasColumnName("agent_run_id");
            entity.Property(r => r.Error).HasColumnName("error").HasMaxLength(2000);
            entity.Property(r => r.UpdatedAtUtc).HasColumnName("updated_at_utc");
            entity.Property(r => r.ExecutionMode).HasColumnName("execution_mode").HasMaxLength(64);
            entity.Property(r => r.RecipeId).HasColumnName("recipe_id");
            entity.Property(r => r.PlanId).HasColumnName("plan_id");
            entity.Property(r => r.ToolKey).HasColumnName("tool_key").HasMaxLength(200);
            entity.Property(r => r.SkillKey).HasColumnName("skill_key").HasMaxLength(200);
            entity.Property(r => r.ToolInputJson).HasColumnName("tool_input_json").HasColumnType("text");
            entity.Property(r => r.ToolPayloadJson).HasColumnName("tool_payload_json").HasColumnType("text");

            entity.HasIndex(r => new { r.Status, r.EnqueuedAtUtc });
        });

        modelBuilder.Entity<ExecutionLeaseRecord>(entity =>
        {
            entity.ToTable("execution_leases");
            entity.HasKey(r => r.ResourceId);
            entity.Property(r => r.ResourceId).HasColumnName("resource_id");
            entity.Property(r => r.LeaseHolder).HasColumnName("lease_holder").IsRequired().HasMaxLength(256);
            entity.Property(r => r.ExpiresAtUtc).HasColumnName("expires_at_utc");
            entity.Property(r => r.CreatedAtUtc).HasColumnName("created_at_utc");
        });

        modelBuilder.Entity<DistributedOperationRecord>(entity =>
        {
            entity.ToTable("distributed_operations");
            entity.HasKey(r => r.OperationKey);
            entity.Property(r => r.OperationKey).HasColumnName("operation_key").HasMaxLength(512);
            entity.Property(r => r.CommittedAtUtc).HasColumnName("committed_at_utc");
        });
    }
}
