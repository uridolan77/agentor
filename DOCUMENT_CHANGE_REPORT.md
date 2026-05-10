# Document Change Report — Agentor Starter Review

I reviewed the current Agentor starter documents and found these needed changes.

## 1. README.md

Current state is good and correctly states the core service boundary:

```text
Agentor = execution runtime
Athanor = canonical knowledge-state / provenance service
Conexus = LLM/model gateway
MCP = later tool/protocol connectivity layer
```

Recommended change:
- Add one short paragraph saying external frameworks are adapters, not core.
- Add a link to `docs/FRAMEWORK_STRATEGY.md`.
- Add a link to `docs/CWC_WORKSHOP_LESSONS_APPLIED.md`.

The current README does not need a full rewrite.

## 2. AGENTS.md

Current issue:
- It says "Keep PRs small and vertical."
- The new request is medium-long passes, not short PRs.

Recommended change:
- Replace with "medium-long, coherent, reviewable passes."
- Add CWC decomposition doctrine.
- Add external frameworks-as-adapters rule.
- Add explicit instruction not to let Microsoft A2A / Semantic Kernel / MCP define Domain.

## 3. PROJECT_CHARTER.md

Current issue:
- Good service-role definition, but it does not explicitly define the framework strategy.

Recommended change:
- Add "Framework compatibility principle."
- Add "Agentor is framework-compatible, not framework-dependent."

## 4. PROJECT_ONTOLOGY_MAP.md

Current issue:
- Good ontology, but should add explicit "framework adapter" concepts.

Recommended change:
- Add:
  - ExternalFrameworkAdapter
  - McpToolBinding
  - A2AExternalAgentCall
  - SemanticKernelAdapter
- Clarify that these are edge concepts, not Domain primitives for PR1–PR40 unless their PR explicitly introduces them.

## 5. docs/ARCHITECTURE.md

Current issue:
- Good Clean Architecture baseline.
- Missing CWC-derived runtime decomposition.

Recommended change:
- Add "CWC decomposition model":
  - tools
  - skills
  - memory
  - evals
  - policies
  - traces
  - external agents later
- Add "frameworks enter through Infrastructure adapters."

## 6. docs/SERVICE_BOUNDARIES.md

Current issue:
- Good Athanor/Conexus/MCP boundary.
- Needs a general external framework section.

Recommended change:
- Add:
  - Microsoft Agent Framework = adapter only
  - Semantic Kernel = adapter only
  - A2A = post-v0.1 external-agent protocol adapter
  - LangGraph/AutoGen/CrewAI = external runtime adapters if ever needed
  - MCP = tool protocol adapter, not core runtime

## 7. docs/ROADMAP.md

Current issue:
- Too short. It has only 8 PRs and does not match the new PR1–PR40 plan.

Recommended change:
- Replace it with a compressed PR1–PR40 roadmap, or make it point to `docs/planning/pr1-pr40/PR_INDEX.md`.

## 8. docs/PR1_BUILD_PLAN.md

Current state:
- Mostly correct.

Recommended change:
- Add one explicit PR1 non-goal:
  - no external agent framework integration
  - no Semantic Kernel
  - no Microsoft Agent Framework
  - no A2A

## 9. decisions/

Recommended additions:
- ADR-006 — External frameworks are adapters, not Agentor core.
- ADR-007 — Anthropic CWC decomposition is adopted as design doctrine.

## 10. PR1–PR40 package

Recommended changes:
- PR10 should include eval fixture baseline earlier.
- PR31–PR35 remain deeper skills/evaluation work.
- PR36–PR37 remain MCP boundary and binding.
- A2A should be post-v0.1, documented as PR41+.
