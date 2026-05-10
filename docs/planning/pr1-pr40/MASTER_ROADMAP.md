# Agentor Master Roadmap — PR1 to PR40

## Design source

Agentor combines:

```text
conexus.adaptation architecture discipline
+ Athanor service-boundary doctrine
+ Anthropic CWC decomposition lessons
+ framework-as-adapter strategy
```

## Key correction from earlier roadmap

Evaluation fixtures appear earlier, in PR10, so later runtime changes can be regression-tested.

A2A is post-v0.1. MCP enters only through tool binding in PR36–PR37.

## PR12.5 — Coordination layer doctrine

PR12.5 (docs/ADR only) records that Agentor owns **coordination** as an explicit runtime layer—separate from Athanor (information and canon), Conexus (model execution), and vendor orchestration graphs (adapters). Runtime policy (PR12) is part of coordination but not the whole layer. Reference coordination configurations and future **evaluation signatures** are documented for roadmap alignment (see `docs/COORDINATION_LAYER.md`, `docs/papers/ARXIV_2605_03310_COORDINATION_LAYER.md`, ADR-008). PR12.5 does not implement coordination code or PR13's tool pipeline.
