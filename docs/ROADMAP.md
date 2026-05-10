# Agentor Roadmap

The active detailed roadmap is:

```text
docs/planning/pr1-pr40/PR_INDEX.md
```

## Summary

```text
PR01–PR05   Runtime kernel and API hardening
PR06–PR10   Persistence, read models, and eval fixture baseline
PR11–PR15   Tools and runtime policy (includes PR12.5 coordination-layer doctrine pass)
PR16–PR20   Plans, recipes, and execution orchestration
PR21–PR25   Athanor integration
PR26–PR30   Conexus integration
PR31–PR35   Skills, memory, and evaluation
PR36–PR40   MCP boundary, observability, deployment, v0.1 RC hardening
PR41+       A2A / external-agent protocols and optional framework adapters
```

## Roadmap principles

- PRs are medium-long, coherent, reviewable passes.
- Agentor owns governed coordination as a runtime layer (see `docs/COORDINATION_LAYER.md`, ADR-008); runtime policy is part of coordination, not the whole layer.
- External frameworks are adapters, not Agentor core.
- Evaluation fixtures appear early and mature over time.

## v0.1 release candidate (Phase 8)

Phase 8 (PR36–PR40) lands the MCP adapter boundary (fake registry + tool binding), API observability hooks, container/CI packaging, and RC versioning. Acceptance is tracked in `.agentor-harness/feature-list.json` with verification in `.agentor-harness/verification-log.md`.