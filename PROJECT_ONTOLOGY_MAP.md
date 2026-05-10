# Agentor Ontology Map

## Core entities

```text
AgentProfile
  describes an executable agent configuration

AgentRun
  one execution instance of an agent profile against an objective

AgentStep
  one ordered step inside a run

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
```

## Later integration entities

```text
AthanorCandidateSubmission
  candidate knowledge sent to Athanor for review

AthanorEvidenceSearch
  evidence-bound retrieval from Athanor

ConexusModelCall
  model invocation routed through Conexus

McpToolBinding
  external tool exposed through MCP
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
```
