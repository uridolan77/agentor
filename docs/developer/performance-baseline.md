# Performance baseline (Phase 39)

This repository treats performance evidence as **local-first** and **non-marketing**:

- Numbers come from **developer machines** or **CI compile-only** checks unless you explicitly run BenchmarkDotNet or load scripts.
- They are **not** production SLOs, scalability guarantees, or competitive benchmarks.

## BenchmarkDotNet (`benchmarks/Agentor.Benchmarks`)

Phase 39 extends the suite with `Phase39RuntimeBenchmarks` (single-tool driver, two-step plan, policy evaluation, audit export, timeline, EF save, queue claim paths, operator diagnostics bundle build).

```powershell
pwsh ./scripts/run-benchmarks.ps1 -- --filter '*Phase39*'
```

CI compiles the benchmark project in Release; it does **not** execute BenchmarkDotNet on GitHub-hosted runners.

## Coordination evaluation vs performance reports

Phase 32 emits **`evaluation-report.{md,json}`** and **`evaluation-summary.csv`** via `scripts/generate-evaluation-ci-artifacts.ps1`.

Phase 39 adds **`performance-report.{md,json}`** and **`performance-summary.csv`** in the same output folder. The performance triple is populated by `PerformanceCiArtifactsTests` with stable placeholder rows for CI determinism; replace with real benchmark medians when publishing a human-maintained baseline.

## Load smoke (`scripts/load-smoke.ps1`)

Parallel HTTP smoke against `/api/v1/agent-runs` (and optional `/api/v1/agent-runs/queued`). Requires PowerShell 7+.

```powershell
pwsh ./scripts/load-smoke.ps1 -StartHost -RunCount 30 -QueueCount 5 -Concurrency 8 -Workload mixed -OutputDirectory ./artifacts/load-smoke
```

Use `-BaseUrl` when the API is already running elsewhere. See also `docs/operator/integration-smoke.md` for integration proof boundaries.

## Persistence stress tests

`tests/Agentor.Infrastructure.Tests/EfPersistenceStressTests.cs` exercises large trace volumes, many tool calls with policy rows, wide plan resume cursors, audit export on heavy runs, and in-memory durable queue listing. These tests assert correctness and shape, not latency targets.
