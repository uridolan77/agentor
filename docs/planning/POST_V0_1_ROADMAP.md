# Agentor post-v0.1 roadmap

## Status

Agentor has completed the original PR1–PR40 sequence through Phase 8:

```text
Phase 1 — Runtime kernel and API hardening
Phase 2 — Persistence, read models, and eval fixture baseline
Phase 3 — Tools and runtime policy
Phase 4 — Plans, recipes, and execution orchestration
Phase 5 — Athanor integration boundary
Phase 6 — Conexus integration boundary
Phase 7 — Skills, memory, and evaluation
Phase 8 — MCP, observability, deployment, and v0.1 release-candidate hardening
```

The next roadmap covers PR41–PR75 and should be treated as **post-v0.1** work.

## Strategic objective

Move Agentor from a v0.1 governed coordination/runtime kernel toward a v1.0 governed agent platform.

Agentor should become capable of:

- invoking external agent/protocol systems without absorbing their ontology;
- using real Athanor, Conexus, MCP, and external-agent transports behind stable ports;
- supporting tenancy, actor context, policy profiles, and review workflows;
- executing durable/background runs safely;
- exposing product-grade APIs and operator surfaces;
- evaluating coordination strategies using deterministic fixtures and quality reports;
- reaching a credible v1.0 release candidate with security, performance, CI/CD, and migration discipline.

## Architectural doctrine

The post-v0.1 roadmap preserves these boundaries:

```text
Agentor coordinates and governs runtime execution.
Athanor owns canonical knowledge, evidence authority, review, and canonization.
Conexus owns model execution and model-provider routing.
MCP tools are adapters exposed as policy-gated Agentor tools.
External agent protocols are adapters exposed as policy-gated Agentor tools.
Session memory is run-scoped scratch, not canon and not Athanor.
Frameworks may be adapters, but never define Agentor core ontology.
```

## Phase map

```text
Phase 9   PR41–PR45 — External agent/protocol adapter layer
Phase 10  PR46–PR50 — Real service adapters and integration modes
Phase 11  PR51–PR55 — Governance, tenancy, security, and human review
Phase 12  PR56–PR60 — Durable execution, queues, reliability, persistence hardening
Phase 13  PR61–PR65 — Product/API surface and operator UX
Phase 14  PR66–PR70 — Advanced evaluation and coordination science
Phase 15  PR71–PR75 — v1.0 platform hardening
```

## Immediate next step

Start with:

```text
PR41 — External agent protocol abstraction
```

Do not implement real A2A, ACP, or other remote protocol transports in PR41. PR41 is a boundary and fake-adapter PR only.

## Cross-phase non-goals

Unless explicitly scheduled in a PR:

- do not introduce real network calls into Domain/Application;
- do not put framework SDK types in Domain;
- do not let external protocols bypass ToolRegistry, RuntimePolicyEvaluator, or ToolExecutionPipeline;
- do not let external-agent output become canonical knowledge;
- do not add arbitrary scripting engines for quality gates or policy gates;
- do not turn MCP, A2A, Semantic Kernel, LangGraph, AutoGen, CrewAI, or Microsoft Agent Framework into Agentor’s internal ontology.
