# Service Boundaries

## Agentor

Owns:
- AgentProfile
- AgentRun
- AgentPlan
- AgentStep
- ToolDefinition
- ToolCall
- PolicyDecision
- ExecutionTraceEvent
- RunManifest
- AgentEvaluationResult
- SkillDefinition
- SkillInvocation
- SessionMemory boundary

Does not own:
- canonical knowledge
- evidence authority
- contradiction resolution
- human review finality
- LLM routing
- MCP marketplace semantics
- external framework ontology

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

MCP may become:
- external tool discovery
- tool capability sync
- tool transport binding

MCP must not become:
- Agentor's Domain model
- Agentor's policy engine
- Agentor's run state machine

Do not introduce MCP in PR1.

## Microsoft Agent Framework / Semantic Kernel

These may be useful later as adapters.

They must not define:
- AgentRun
- AgentPlan
- ToolCall
- PolicyDecision
- ExecutionTrace
- RunManifest

Possible future adapters:
- `Agentor.SemanticKernel.Adapter`
- `Agentor.MicrosoftAgent.Adapter`

## A2A / external-agent protocols

A2A belongs in a future external-agent communication layer.

Potential future concepts:
- ExternalAgentDefinition
- ExternalAgentCall
- AgentMessageEnvelope
- RemoteAgentPolicyDecision
- ExternalAgentTraceRef

A2A is not part of PR1–PR40 v0.1 unless a concrete requirement appears.
