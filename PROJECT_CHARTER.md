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

Agentor does not own:
- canonical knowledge
- evidence truth
- contradiction resolution
- project-local epistemic authority
- model routing
- model pricing
- protocol marketplaces

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

## First product milestone

A single deterministic agent run can be started, traced, completed, retrieved, and represented as a run manifest.
