# Architecture boundary review (v1.0 RC)

Aligned with `docs/GOVERNANCE_BOUNDARY.md` and project doctrine:

| Boundary | Rule |
|----------|------|
| Agentor | Coordinates runs, steps, tools, policy, traces, manifests, eval harness artifacts. |
| Athanor | Canonical knowledge state and provenance; Agentor never replaces Athanor canon. |
| Conexus | Model routing/gateway; Agentor does not become an LLM marketplace. |
| MCP / frameworks | Adapters and transports; not core runtime ownership. |
| Session memory | Scratch/bounded working set; not canonical knowledge. |
| External agents | Output is evidence/candidate until explicitly submitted/reviewed per governance; non-canon by default. |

This document is the Phase 75 exit architecture checklist companion to Phase 15 planning.
## PR75.6 documentation note

This file is unchanged in substance for PR75.6. RC means **review-ready** boundaries and deferred items are tracked in `docs/RELEASE/v1.0-RC-DEFERRED-ITEMS.md` rather than being silently dropped from the harness.