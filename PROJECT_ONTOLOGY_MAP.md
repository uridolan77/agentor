# Agentor Ontology Map

## Core entities

```text
AgentProfile
  describes an executable agent configuration

AgentRun
  one execution instance of an agent profile against an objective

AgentPlan
  an execution plan composed of ordered/guarded steps

AgentStep
  one ordered step inside a run or plan

ToolDefinition
  a tool capability known to the runtime

ToolCall
  one attempted invocation of a tool

PolicyDecision
  allow / deny / require review decision before an action

ExecutionTraceEvent
  append-only-style observation emitted during execution

RunManifest
  reproducible summary of what happened in a run

AgentEvaluationResult
  measured quality/safety/cost/latency result for a run

SkillDefinition
  reusable procedural knowledge package, not directly a tool

SkillInvocation
  invocation of a skill within a run or plan

SessionMemory
  bounded run/session context, not canonical knowledge
```

## Coordination layer (doctrine and future concepts)

Coordination is how governed work is organized to completion. Agentor owns coordination as part of its runtime, separate from canonical knowledge (Athanor) and model execution (Conexus). External frameworks provide adapters, not Agentor's coordination ontology (ADR-006, ADR-008).

Future coordination concepts (names indicative; not all exist in code yet):

```text
CoordinationProfile
  declarative configuration of topology, authority, aggregation, sync, termination, failure handling, budget, and evaluation signature expectations for a run or plan

CoordinationEvaluationSignature
  expected and observed behavioral signatures for a coordination pattern (reliability/calibration, resolution, cost/latency, token/compute, diversity collapse, escalation, failure isolation, termination quality)

ComputeBudgetProfile
  limits and accounting for steps, tool calls, model calls, tokens, cost, latency, review escalations
```

Reference coordination patterns for design and evaluation (not normative defaults): Single Agent, Sequential Pipeline, Independent Ensemble, Peer-Critique Debate, Orchestrator-Specialist, Consensus Alignment. See `docs/COORDINATION_LAYER.md` and `docs/papers/ARXIV_2605_03310_COORDINATION_LAYER.md`.

## Integration entities

```text
AthanorCandidateSubmission
  candidate knowledge sent to Athanor for review

AthanorEvidenceSearch
  evidence-bound retrieval from Athanor

ConexusModelCall
  model invocation routed through Conexus

McpToolBinding
  external tool exposed through MCP

ExternalFrameworkAdapter
  adapter wrapper around an external agent framework

A2AExternalAgentCall
  future governed call to an external agent through A2A-like protocol

SemanticKernelAdapter
  future adapter for prompt/function abstractions, not core runtime
```

## Forbidden ontology collapse

Do not collapse:

```text
Tool output ≠ knowledge
LLM output ≠ knowledge
Agent proposal ≠ canonical state
Execution trace ≠ review event
Run manifest ≠ canonical snapshot
Agentor policy ≠ Athanor authority
Skill ≠ tool
Memory ≠ Athanor
MCP ≠ Agentor core
A2A ≠ Agentor core
Semantic Kernel ≠ Agentor core
Microsoft Agent Framework ≠ Agentor core
```
