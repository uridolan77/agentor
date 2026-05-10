# Agentor Project Charter

## Mission

Agentor provides a .NET runtime for executing agents in a way that is deterministic enough to test, observable enough to audit, and policy-governed enough to integrate safely with knowledge and model systems.

## Core claim

The primitive is not "an agent."  
The primitive is an evaluable execution run.

## Service role

Agentor owns:
- agent profiles
- execution plans
- agent runs
- steps
- tool calls
- policy decisions
- execution traces
- run manifests
- run-level evaluation
- skills as procedural runtime packages
- runtime memory boundaries
- coordination as an explicit governed runtime layer (topology, authority, aggregation, synchronization, termination, failure handling, budgets, and evaluation signatures over time; see `docs/COORDINATION_LAYER.md` and ADR-008)

Agentor does not own:
- canonical knowledge
- evidence truth
- contradiction resolution
- project-local epistemic authority
- model routing
- model pricing
- protocol marketplaces
- external framework ontology

## External services

Athanor:
- canonical knowledge-state service
- evidence binding
- review events
- contradictions
- canonical snapshots
- project-local authority

Conexus:
- LLM/model gateway
- model routing
- adapter profiles
- quality/cost/latency policy for model calls

MCP:
- future tool/protocol connectivity layer

A2A / external-agent protocols:
- future external-agent communication layer
- not part of v0.1 core

Microsoft Agent Framework / Semantic Kernel / LangGraph / AutoGen / CrewAI:
- possible adapter integrations
- not Agentor's core runtime ontology

## Framework compatibility principle

Agentor should be framework-compatible, not framework-dependent.

Frameworks may plug into Agentor through Infrastructure adapters after the runtime model is stable. They must not define Agentor's Domain primitives.

## First product milestone

A single deterministic agent run can be started, traced, completed, retrieved, and represented as a run manifest.
