# Service Boundaries

## Agentor

Owns:
- AgentProfile
- AgentRun
- AgentStep
- ToolDefinition
- ToolCall
- PolicyDecision
- ExecutionTraceEvent
- RunManifest
- AgentEvaluationResult

Does not own:
- canonical knowledge
- evidence authority
- contradiction resolution
- human review finality
- LLM routing
- MCP marketplace semantics

## Athanor

Athanor remains the knowledge-state service.

Agentor may:
- read canonical snapshots
- search evidence-bound state
- submit candidate knowledge
- queue review items
- attach execution provenance

Agentor may not:
- canonize
- accept object versions
- resolve contradictions
- create authoritative snapshots
- bypass project-local authority

## Conexus

Conexus remains the model gateway.

Agentor may later:
- request model completions
- ask for model routing
- use adapter profiles
- receive model-call telemetry

Agentor should not embed provider-specific model logic in Domain/Application.

## MCP

MCP comes later as transport/tool discovery.

Do not introduce MCP in PR1.
