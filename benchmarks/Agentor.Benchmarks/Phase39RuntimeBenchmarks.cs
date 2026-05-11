using Agentor.Api.Diagnostics;
using Agentor.Api.Security;
using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Application.Commands;
using Agentor.Application.Coordination;
using Agentor.Application.Options;
using Agentor.Application.Orchestration;
using Agentor.Application.Queries;
using Agentor.Application.RunQueue;
using Agentor.Contracts;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Policy;
using Agentor.Infrastructure;
using Agentor.Infrastructure.Conexus;
using Agentor.Infrastructure.ExternalAgents;
using Agentor.Infrastructure.Management;
using Agentor.Infrastructure.Mcp;
using Agentor.Infrastructure.Options;
using Agentor.Infrastructure.Persistence;
using Agentor.Infrastructure.Policy;
using Agentor.Infrastructure.RunQueue;
using BenchmarkDotNet.Attributes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Agentor.Benchmarks;

/// <summary>Phase 39 PR159 — local micro-benchmarks (not production SLOs).</summary>
[MemoryDiagnoser]
public class Phase39RuntimeBenchmarks
{
    private const string FakeTool = WellKnownToolKeys.Pr1FakeTool;

    private InMemoryAgentRunRepository _memoryRepo = null!;
    private GovernedSingleToolRunDriver _driver = null!;
    private SequentialAgentPlanExecutor _planExecutor = null!;
    private AgentRun _planRun = null!;
    private AgentPlan _twoStepPlan = null!;
    private RuntimePolicyEvaluator _policy = null!;
    private PolicyEvaluationRequest _policyRequest = null!;
    private GetRunAuditExportQueryHandler _auditHandler = null!;
    private AgentRun _auditRun = null!;
    private GetRunTimelineQueryHandler _timelineHandler = null!;
    private AgentRun _timelineRun = null!;
    private OperatorDiagnosticsService _diagnostics = null!;
    private EfCoreAgentRunRepository _efRepo = null!;
    private AgentorDbContext _efCtx = null!;
    private AgentRun _efSaveRun = null!;
    private EfRunQueueStore _efQueue = null!;
    private AgentorDbContext _queueCtx = null!;
    private SqliteConnection _queueConnection = null!;
    private InMemoryDurableRunQueueStore _memQueue = null!;

    [GlobalSetup]
    public void Setup()
    {
        var clock = new SystemClock();
        var now = clock.UtcNow;

        var fake = new FakeToolExecutor();
        var registry = ToolRegistry.CreateDefault(fake, new FakeModelGatewayClient(), new FakeMcpRegistryClient(), new FakeA2AExternalAgentClient());
        var policyOpts = Options.Create(new RuntimePolicyOptions());
        _policy = new RuntimePolicyEvaluator(registry, clock, policyOpts);
        var pipeline = new ToolExecutionPipeline(clock, Options.Create(new ToolExecutionOptions()));
        _memoryRepo = new InMemoryAgentRunRepository();
        _driver = new GovernedSingleToolRunDriver(_memoryRepo, _policy, registry, pipeline, clock);

        var recipeOk = AgentRecipe.TryCreate(
            Guid.NewGuid(),
            "bench-two",
            AgentRecipeVersion.Parse("1.0"),
            CoordinationTopology.SequentialPipeline,
            [
                new RecipeStepDefinition("a", 1, RecipeStepKind.Tool, FakeTool, OnFailure: FailureHandlingPolicy.FailFast),
                new RecipeStepDefinition("b", 2, RecipeStepKind.Tool, FakeTool, OnFailure: FailureHandlingPolicy.FailFast)
            ],
            null,
            out var recipe,
            out _);
        if (!recipeOk || recipe is null)
        {
            throw new InvalidOperationException("bench recipe");
        }

        _planRun = AgentRun.Start(Guid.NewGuid(), "BenchPlan", "obj", "bench-plan-trace", now);
        _twoStepPlan = AgentPlan.Instantiate(recipe, Guid.NewGuid(), now);
        var guards = new StepGuardEvaluator();
        _planExecutor = new SequentialAgentPlanExecutor(registry, _policy, pipeline, clock, guards, new InMemorySkillPackageCatalog());

        _policyRequest = new PolicyEvaluationRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            FakeTool,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            null,
            new AgentRunScope(null, null, null, null));

        _auditRun = AgentRun.Start(Guid.NewGuid(), "BenchAudit", "obj", "bench-audit", now);
        var st = _auditRun.StartStep("s", now);
        var tc = ToolCall.Start(_auditRun.Id, st.Id, FakeTool, new Dictionary<string, string>(), now);
        st.AddToolCall(tc);
        tc.Succeed(new Dictionary<string, string> { ["r"] = "ok" }, now);
        st.Complete(now);
        _auditRun.Complete(now);
        _memoryRepo.SaveAsync(_auditRun, CancellationToken.None).GetAwaiter().GetResult();
        _auditHandler = new GetRunAuditExportQueryHandler(_memoryRepo, Options.Create(new AuditExportOptions()));

        _timelineRun = AgentRun.Start(Guid.NewGuid(), "BenchTimeline", "obj", "bench-tl", now);
        var tlStep = _timelineRun.StartStep("tl", now);
        for (var i = 0; i < 40; i++)
        {
            _timelineRun.RecordTrace(TraceEventKind.ToolCallStarted, $"e{i}", now.AddTicks(i));
        }

        tlStep.Complete(now.AddMinutes(1));
        _timelineRun.Complete(now.AddMinutes(2));

        _timelineHandler = new GetRunTimelineQueryHandler(_memoryRepo);
        _memoryRepo.SaveAsync(_timelineRun, CancellationToken.None).GetAwaiter().GetResult();

        _efCtx = new AgentorDbContext(
            new DbContextOptionsBuilder<AgentorDbContext>()
                .UseInMemoryDatabase("phase39-bench-ef-" + Guid.NewGuid().ToString("N"))
                .Options);
        _efRepo = new EfCoreAgentRunRepository(_efCtx);
        _efSaveRun = BuildSmallCompletedRun("ef-bench-save");
        _efRepo.SaveAsync(_efSaveRun, CancellationToken.None).GetAwaiter().GetResult();

        _queueConnection = new SqliteConnection("DataSource=:memory:");
        _queueConnection.Open();
        _queueCtx = new AgentorDbContext(
            new DbContextOptionsBuilder<AgentorDbContext>()
                .UseSqlite(_queueConnection)
                .Options);
        _queueCtx.Database.EnsureCreated();
        _efQueue = new EfRunQueueStore(_queueCtx);
        for (var q = 0; q < 50; q++)
        {
            var wi = new RunWorkItem(Guid.NewGuid(), new StartAgentRunCommand("Q", "bench"));
            _efQueue.EnqueueAsync(wi, now, CancellationToken.None).GetAwaiter().GetResult();
        }

        _memQueue = new InMemoryDurableRunQueueStore();

        var runsForDiag = new InMemoryAgentRunRepository();
        var diagRun = AgentRun.Start(Guid.NewGuid(), "D", "o", "d-trace", now);
        var ds = diagRun.StartStep("s", now);
        ds.Complete(now);
        diagRun.Complete(now);
        runsForDiag.SaveAsync(diagRun, CancellationToken.None).GetAwaiter().GetResult();

        var queue = new InMemoryDurableRunQueueStore();
        queue.EnqueueAsync(new RunWorkItem(Guid.NewGuid(), new StartAgentRunCommand("Dq", "x")), now, CancellationToken.None).GetAwaiter().GetResult();
        var outbox = new InMemoryOutboxStore();
        var integration = new BenchIntegrationReader();
        var profiles = new InMemoryPolicyProfileRepository();
        var bundles = new InMemoryPolicyBundleRepository();
        var runtime = new FrozenMonitor<AgentorRuntimeOptions>(new AgentorRuntimeOptions());
        var persistence = new FrozenMonitor<AgentorPersistenceOptions>(new AgentorPersistenceOptions { Mode = "InMemory" });
        var auth = new FrozenMonitor<AgentorAuthOptions>(new AgentorAuthOptions());
        var worker = new FrozenMonitor<RunWorkerOptions>(new RunWorkerOptions());
        var outboxDispatch = new FrozenMonitor<OutboxDispatchOptions>(new OutboxDispatchOptions());
        var runQueue = new FrozenMonitor<RunQueueOptions>(new RunQueueOptions());
        var env = new BenchHostEnvironment();
        _diagnostics = new OperatorDiagnosticsService(
            runsForDiag,
            queue,
            outbox,
            integration,
            profiles,
            bundles,
            runtime,
            persistence,
            auth,
            worker,
            outboxDispatch,
            runQueue,
            env);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _efCtx?.Dispose();
        _queueCtx?.Dispose();
        _queueConnection?.Dispose();
    }

    [Benchmark]
    public async Task SingleTool_DriverExecuteAsync()
    {
        var req = new RunOrchestrationRequest(
            AgentName: "B",
            Objective: "o",
            TraceId: "bench-st-" + Guid.NewGuid().ToString("N"),
            TenantId: null,
            WorkspaceId: null,
            ProjectId: null,
            KnowledgeScopeId: null,
            Mode: RunExecutionMode.LegacyFakeTool,
            RecipeId: null,
            PlanId: null,
            ToolKey: FakeTool,
            SkillKey: null,
            ToolInput: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        _ = await _driver.ExecuteAsync(req, "p", "sum", FakeTool, CancellationToken.None).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task Plan_TwoStepExecuteAsync()
    {
        var run = AgentRun.Start(Guid.NewGuid(), "BenchPlan", "obj", "tp-" + Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow);
        _ = await _planExecutor.ExecuteAsync(run, _twoStepPlan, CancellationToken.None).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task Policy_EvaluateToolCallAsync()
    {
        _ = await _policy.EvaluateToolCallAsync(_policyRequest, CancellationToken.None).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task AuditExport_HandleAsync()
    {
        _ = await _auditHandler.HandleAsync(_auditRun.Id, CancellationToken.None).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task Timeline_HandleAsync()
    {
        _ = await _timelineHandler.HandleAsync(_timelineRun.Id, CancellationToken.None).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task Queue_EfTryClaimNextAsync()
    {
        _ = await _efQueue.TryClaimNextAsync("bench-worker", TimeSpan.FromMinutes(2), DateTimeOffset.UtcNow, CancellationToken.None)
            .ConfigureAwait(false);
    }

    [Benchmark]
    public async Task EfSave_CompletedRunClone()
    {
        var r = BuildSmallCompletedRun("clone-" + Guid.NewGuid().ToString("N"));
        await _efRepo.SaveAsync(r, CancellationToken.None).ConfigureAwait(false);
    }

    [IterationSetup(Target = nameof(Queue_MemTryClaimNext))]
    public void MemQueueIterationSetup()
    {
        _memQueue = new InMemoryDurableRunQueueStore();
        var wi = new RunWorkItem(Guid.NewGuid(), new StartAgentRunCommand("M", "q"));
        _memQueue.EnqueueAsync(wi, DateTimeOffset.UtcNow, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Benchmark]
    public async Task Queue_MemTryClaimNext()
    {
        _ = await _memQueue.TryClaimNextAsync("w", TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow, CancellationToken.None)
            .ConfigureAwait(false);
    }

    [Benchmark]
    public async Task Diagnostics_BuildAsync()
    {
        _ = await _diagnostics.BuildAsync(false, CancellationToken.None).ConfigureAwait(false);
    }

    private static AgentRun BuildSmallCompletedRun(string traceId)
    {
        var clock = new SystemClock();
        var now = clock.UtcNow;
        var profile = AgentProfile.Create("E", "o", now);
        var run = AgentRun.Start(profile.Id, profile.Name, "o", traceId, now);
        var step = run.StartStep("s", now);
        step.Complete(now);
        run.Complete(now);
        return run;
    }

    private sealed class BenchIntegrationReader : IIntegrationStatusReader
    {
        public Task<IntegrationsStatusResponseDto> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            var dict = new Dictionary<string, IntegrationAdapterStatusDto>(StringComparer.OrdinalIgnoreCase)
            {
                ["athanor"] = new IntegrationAdapterStatusDto("Fake", true, null),
                ["conexus"] = new IntegrationAdapterStatusDto("Fake", true, null),
            };
            return Task.FromResult(new IntegrationsStatusResponseDto(true, dict, null));
        }
    }

    private sealed class BenchHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Agentor.Benchmarks";

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FrozenMonitor<T> : IOptionsMonitor<T>
        where T : class, new()
    {
        public FrozenMonitor(T value) => CurrentValue = value;

        public T CurrentValue { get; }

        public T Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<T, string?> listener) => NullDisp.Instance;

        private sealed class NullDisp : IDisposable
        {
            public static readonly NullDisp Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
