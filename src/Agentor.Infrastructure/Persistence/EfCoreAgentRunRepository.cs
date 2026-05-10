using System.Text.Json;
using Agentor.Application.Abstractions;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Agentor.Infrastructure.Persistence;

public sealed class EfCoreAgentRunRepository : IAgentRunRepository
{
    private readonly AgentorDbContext _context;

    public EfCoreAgentRunRepository(AgentorDbContext context)
    {
        _context = context;
    }

    public async Task SaveAsync(AgentRun run, CancellationToken cancellationToken)
    {
        var existing = await _context.AgentRuns
            .Include(r => r.Steps)
            .ThenInclude(s => s.ToolCalls)
            .Include(r => r.Steps)
            .ThenInclude(s => s.PolicyDecisions)
            .Include(r => r.TraceEvents)
            .FirstOrDefaultAsync(r => r.Id == run.Id, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            var record = RecordMapper.ToRecord(run);
            await _context.AgentRuns.AddAsync(record, cancellationToken).ConfigureAwait(false);
            try
            {
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new AgentRunPersistenceConcurrencyException(run.Id);
            }

            var insertedVersion = _context.Entry(record).Property(r => r.AggregateVersion).CurrentValue;
            run.PersistenceConcurrencyVersion = (long)insertedVersion;
            return;
        }

        var expectedVersion = run.PersistenceConcurrencyVersion ?? existing.AggregateVersion;
        if (expectedVersion != existing.AggregateVersion)
        {
            throw new AgentRunPersistenceConcurrencyException(run.Id);
        }

        var entry = _context.Entry(existing);
        entry.Property(e => e.AggregateVersion).OriginalValue = expectedVersion;
        existing.AggregateVersion = expectedVersion + 1;

        existing.ProfileId = run.ProfileId;
        existing.TenantId = run.TenantId;
        existing.WorkspaceId = run.WorkspaceId;
        existing.ProjectId = run.ProjectId;
        existing.KnowledgeScopeId = run.KnowledgeScopeId;
        existing.AgentName = run.AgentName;
        existing.Objective = run.Objective;
        existing.TraceId = run.TraceId;
        existing.Status = run.Status.ToString();
        existing.StartedAt = run.StartedAt;
        existing.CompletedAt = run.CompletedAt;
        existing.ErrorMessage = run.ErrorMessage;
        existing.SessionMemoryJson = JsonSerializer.Serialize(run.SessionMemory, RecordMapper.RecordJsonOptions);
        existing.HumanReviewDecisionsJson = JsonSerializer.Serialize(run.HumanReviewDecisions.ToList(), RecordMapper.RecordJsonOptions);
        existing.ResumeCursorJson = run.ResumeCursor is null
            ? null
            : JsonSerializer.Serialize(run.ResumeCursor, RecordMapper.RecordJsonOptions);

        SyncSteps(existing, run);
        AppendTraceEvents(existing, run);

        try
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AgentRunPersistenceConcurrencyException(run.Id);
        }

        run.PersistenceConcurrencyVersion = existing.AggregateVersion;
    }

    public async Task<AgentRun?> GetAsync(Guid runId, CancellationToken cancellationToken)
    {
        var record = await _context.AgentRuns
            .AsNoTracking()
            .Include(r => r.Steps)
            .ThenInclude(s => s.ToolCalls)
            .Include(r => r.Steps)
            .ThenInclude(s => s.PolicyDecisions)
            .Include(r => r.TraceEvents)
            .FirstOrDefaultAsync(r => r.Id == runId, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            return null;
        }

        var run = RecordMapper.ToDomain(record);
        run.PersistenceConcurrencyVersion = record.AggregateVersion;
        return run;
    }

    public async Task<AgentRunListPage> ListSummariesAsync(
        int skip,
        int take,
        CancellationToken cancellationToken,
        AgentRunStatus? statusFilter = null)
    {
        var query = _context.AgentRuns.AsNoTracking();
        if (statusFilter is not null)
        {
            var statusString = statusFilter.Value.ToString();
            query = query.Where(r => r.Status == statusString);
        }

        query = query
            .OrderByDescending(r => r.StartedAt)
            .ThenBy(r => r.Id);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = rows.Select(RecordMapper.ToSummary).ToList();
        return new AgentRunListPage(items, total, skip, take);
    }

    private static void SyncSteps(AgentRunRecord existing, AgentRun run)
    {
        foreach (var domainStep in run.Steps)
        {
            var stepRow = existing.Steps.FirstOrDefault(s => s.Id == domainStep.Id);
            if (stepRow is null)
            {
                stepRow = new AgentStepRecord
                {
                    Id = domainStep.Id,
                    RunId = run.Id,
                    Index = domainStep.Index,
                    Name = domainStep.Name,
                    Status = domainStep.Status.ToString(),
                    StartedAt = domainStep.StartedAt,
                    CompletedAt = domainStep.CompletedAt,
                    Run = existing,
                };
                existing.Steps.Add(stepRow);
            }
            else
            {
                stepRow.Index = domainStep.Index;
                stepRow.Name = domainStep.Name;
                stepRow.Status = domainStep.Status.ToString();
                stepRow.StartedAt = domainStep.StartedAt;
                stepRow.CompletedAt = domainStep.CompletedAt;
            }

            SyncToolCalls(stepRow, domainStep);
            SyncPolicyDecisions(stepRow, domainStep);
        }
    }

    private static void SyncToolCalls(AgentStepRecord stepRow, AgentStep domainStep)
    {
        foreach (var tc in domainStep.ToolCalls)
        {
            var row = stepRow.ToolCalls.FirstOrDefault(t => t.Id == tc.Id);
            if (row is null)
            {
                row = new ToolCallRecord
                {
                    Id = tc.Id,
                    RunId = domainStep.RunId,
                    StepId = domainStep.Id,
                    ToolKey = tc.ToolKey,
                    Status = tc.Status.ToString(),
                    InputJson = JsonSerializer.Serialize(tc.Input, RecordMapper.RecordJsonOptions),
                    OutputJson = JsonSerializer.Serialize(tc.Output, RecordMapper.RecordJsonOptions),
                    StartedAt = tc.StartedAt,
                    CompletedAt = tc.CompletedAt,
                    ErrorMessage = tc.ErrorMessage,
                    Step = stepRow,
                };
                stepRow.ToolCalls.Add(row);
            }
            else
            {
                row.ToolKey = tc.ToolKey;
                row.Status = tc.Status.ToString();
                row.InputJson = JsonSerializer.Serialize(tc.Input, RecordMapper.RecordJsonOptions);
                row.OutputJson = JsonSerializer.Serialize(tc.Output, RecordMapper.RecordJsonOptions);
                row.StartedAt = tc.StartedAt;
                row.CompletedAt = tc.CompletedAt;
                row.ErrorMessage = tc.ErrorMessage;
            }
        }
    }

    private static void SyncPolicyDecisions(AgentStepRecord stepRow, AgentStep domainStep)
    {
        foreach (var pd in domainStep.PolicyDecisions)
        {
            var row = stepRow.PolicyDecisions.FirstOrDefault(p => p.Id == pd.Id);
            if (row is null)
            {
                row = new PolicyDecisionRecord
                {
                    Id = pd.Id,
                    RunId = domainStep.RunId,
                    StepId = domainStep.Id,
                    Outcome = pd.Outcome.ToString(),
                    ReasonCode = pd.ReasonCode,
                    Reason = pd.Reason,
                    DecidedAt = pd.DecidedAt,
                    Step = stepRow,
                };
                stepRow.PolicyDecisions.Add(row);
            }
            else
            {
                row.Outcome = pd.Outcome.ToString();
                row.ReasonCode = pd.ReasonCode;
                row.Reason = pd.Reason;
                row.DecidedAt = pd.DecidedAt;
            }
        }
    }

    private void AppendTraceEvents(AgentRunRecord existing, AgentRun run)
    {
        var byId = existing.TraceEvents.ToDictionary(e => e.Id);
        foreach (var evt in run.Trace)
        {
            if (byId.TryGetValue(evt.Id, out var row))
            {
                if (!TracePayloadEquals(row, evt))
                {
                    throw new AgentRunTraceImmutabilityException(run.Id, evt.Id);
                }
            }
            else
            {
                var added = RecordMapper.ToTraceRecord(evt);
                added.Run = existing;
                existing.TraceEvents.Add(added);
                byId[evt.Id] = added;
            }
        }
    }

    private static bool TracePayloadEquals(TraceEventRecord row, ExecutionTraceEvent evt)
    {
        if (!string.Equals(row.Kind, evt.Kind.ToString(), StringComparison.Ordinal)
            || !string.Equals(row.Message, evt.Message, StringComparison.Ordinal)
            || row.OccurredAt != evt.OccurredAt)
        {
            return false;
        }

        var rowCanon = JsonSerializer.Serialize(
            JsonSerializer.Deserialize<Dictionary<string, string>>(row.DataJson, RecordMapper.RecordJsonOptions)
            ?? new Dictionary<string, string>(),
            RecordMapper.RecordJsonOptions);
        var evtCanon = JsonSerializer.Serialize(evt.Data, RecordMapper.RecordJsonOptions);
        return string.Equals(rowCanon, evtCanon, StringComparison.Ordinal);
    }
}
