# Agentor.Benchmarks

BenchmarkDotNet harness for Phase 15 and Phase 39 runtime micro-benchmarks.

## CI vs local

- **CI** (`/.github/workflows/ci.yml`): compiles `benchmarks/Agentor.Benchmarks` in **Release** only — no benchmark execution on the build agent.
- **Local**: run the full harness when you need medians or before/after comparisons on a fixed machine.

## Run locally

```powershell
pwsh ./scripts/run-benchmarks.ps1 -- --filter '*'
```

Phase 39 slice:

```powershell
pwsh ./scripts/run-benchmarks.ps1 -- --filter '*Phase39*'
```

Equivalent manual command:

```powershell
dotnet run -c Release --project benchmarks/Agentor.Benchmarks/Agentor.Benchmarks.csproj -- --filter '*'
```

See `docs/developer/performance-baseline.md` (Phase 39) and `docs/developer/phase15-performance-baselines.md`.
