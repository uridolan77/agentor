# Phase 15 — performance baselines

Targets from `docs/planning/pr41-pr75/PHASE_15_V1_HARDENING.md` (PR72):

- Run creation
- Plan execution
- Manifest generation
- Timeline query
- Audit packet export
- Evaluation fixture run

## Harness project

`benchmarks/Agentor.Benchmarks` (BenchmarkDotNet). Build:

```powershell
dotnet build benchmarks/Agentor.Benchmarks -c Release
dotnet run -c Release --project benchmarks/Agentor.Benchmarks -- --filter '*'
```

Local developer machines produce **median / mean** rows in BenchmarkDotNet output; CI compiles the benchmark project in Release to guard drift.

## Regression policy

- **Do not** gate CI on absolute nanosecond thresholds (hardware-dependent).
- Use BenchmarkDotNet trends locally when investigating regressions; compare baselines before/after change on the same machine configuration.
- Smoke expectation: CI benchmark **compile** step ensures the harness stays buildable.

## Documented reference (replace with your local median after `dotnet run`)

| Scenario | Notes |
|----------|------|
| AuditExport_HandleAsync | In-memory repo + completed run |
| Manifest_FromRun | `RunManifest.FromRun` + telemetry aggregators |

Record medians from BenchmarkDotNet summary table after a Release run.