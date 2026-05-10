# Phase 15 — performance baselines

Targets from `docs/planning/pr41-pr75/PHASE_15_V1_HARDENING.md` (PR72):

- Run creation
- Plan execution
- Manifest generation
- Timeline query
- Audit packet export
- Evaluation fixture run

## Harness project

`benchmarks/Agentor.Benchmarks` (BenchmarkDotNet).

### CI (compile-only)

GitHub Actions runs:

```powershell
dotnet build benchmarks/Agentor.Benchmarks/Agentor.Benchmarks.csproj --configuration Release --no-restore
```

This proves the harness **compiles** against current contracts; it does **not** collect timings.

### Local execution (timings)

Use the repo helper (same as `dotnet run`):

```powershell
pwsh ./scripts/run-benchmarks.ps1 -- --filter '*'
```

Or:

```powershell
dotnet run -c Release --project benchmarks/Agentor.Benchmarks/Agentor.Benchmarks.csproj -- --filter '*'
```

## Regression policy

- **Do not** gate CI on absolute nanosecond thresholds (hardware-dependent); CI only enforces **compile health** for the benchmark project.
- Use BenchmarkDotNet trends locally when investigating regressions; compare baselines before/after change on the same machine configuration.
- Future work may snapshot medians into this doc or add a separate perf gate — **not enforced** in v1.0 RC.

## Documented reference (replace with your local median after `dotnet run`)

| Scenario | Notes |
|----------|------|
| AuditExport_HandleAsync | In-memory repo + completed run |
| Manifest_FromRun | `RunManifest.FromRun` + telemetry aggregators |

Record medians from BenchmarkDotNet summary table after a Release run.