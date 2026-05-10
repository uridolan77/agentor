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
            entity.Property(r => r.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
            entity.Property(r => r.SessionMemoryJson).HasColumnName("session_memory_json").IsRequired();
            entity.Property(r => r.HumanReviewDecisionsJson).HasColumnName("human_review_decisions_json").IsRequired();

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
    }
}
