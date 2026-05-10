# Phase 9 — External agent and protocol adapter layer

## Purpose

Add an external-agent/protocol adapter layer while preserving Agentor’s core ontology.

External agent systems may be invoked by Agentor, but they must be represented as **policy-gated tools/adapters**, not as first-class replacements for Agentor plans, runs, steps, policy decisions, traces, or manifests.

## Doctrine

```text
External agents are adapters.
External protocols are not Agentor ontology.
External-agent output is tool output/evidence, not canonical knowledge.
External-agent calls must pass through ToolRegistry, RuntimePolicyEvaluator, and ToolExecutionPipeline.
RequiresReview must not invoke external agents.
```

## PR41 — External agent protocol abstraction

### Goal

Introduce a generic boundary for external-agent protocols without implementing any real remote protocol.

### Add

```text
src/Agentor.Application/Abstractions/IExternalAgentProtocolClient.cs
src/Agentor.Contracts/ExternalAgents/ExternalAgentDtos.cs
src/Agentor.Infrastructure/ExternalAgents/FakeExternalAgentProtocolClient.cs
tests/Agentor.Infrastructure.Tests/FakeExternalAgentProtocolClientTests.cs
docs/EXTERNAL_AGENT_PROTOCOL_BOUNDARY.md
```

### Suggested types

```text
IExternalAgentProtocolClient
ExternalAgentProtocolKind
ExternalAgentCapabilityDto
ExternalAgentInvocationRequestDto
ExternalAgentInvocationResultDto
ExternalAgentInvocationStatus
ExternalAgentIdentityDto
```

### Acceptance

- Application port exists.
- Fake implementation is deterministic.
- No real A2A, ACP, HTTP, WebSocket, or network transport.
- No protocol SDK types in Domain.
- External-agent results are explicitly non-canon.

### Non-goals

- No real A2A.
- No external-agent tool registration yet.
- No UI.
- No remote network calls.

## PR42 — Fake A2A-style adapter

### Goal

Model A2A-like discovery and invocation semantics behind the generic external-agent boundary.

### Add

```text
FakeA2AExternalAgentClient
A2AAgentCardDto
A2ACapabilityDto
A2AInvocationMetadataDto
```

### Acceptance

- Fake agent card discovery works.
- Fake invocation produces deterministic result.
- A2A-like data structures remain in Contracts/Infrastructure, not Domain.
- No real A2A network implementation.

### Non-goals

- No conformance claim to any evolving A2A standard.
- No agent-to-agent runtime topology.
- No consensus/debate/orchestrator topology.

## PR43 — External-agent tool binding

### Goal

Expose external-agent discovery/invocation as Agentor tools.

### Add

```text
ExternalAgentToolKeys
ExternalAgentDiscoverToolExecutor
ExternalAgentInvokeToolExecutor
ToolRegistry binding
Runtime policy tests
```

### Tool keys

```text
external-agent.discover
external-agent.invoke
```

### Acceptance

- Tools are registered through ToolRegistry.
- RuntimePolicyEvaluator gates calls.
- ToolExecutionPipeline executes calls.
- Deny and RequiresReview do not invoke external agents.
- ToolResultEnvelope contains protocol kind, agent key, capability, status.

### Non-goals

- No automatic Athanor candidate submission.
- No external-agent output canonization.
- No UI.

## PR44 — External-agent audit and provenance surfaces

### Goal

Make external-agent calls visible in traces, manifests, audit/read models, and evaluation snapshots.

### Suggested trace kinds

```text
ExternalAgentCapabilityDiscovered
ExternalAgentInvocationStarted
ExternalAgentInvocationCompleted
ExternalAgentInvocationDenied
ExternalAgentInvocationRequiresReview
ExternalAgentInvocationFailed
```

### Acceptance

- Read model exposes protocol kind, external agent ID/key, capability key, policy outcome, invocation status, trace IDs.
- Manifest includes external-agent call count and basic non-sensitive metadata.
- Audit packet distinguishes external-agent output from canonical knowledge.

### Non-goals

- No human review workflow; that comes later.
- No real protocol transport.

## PR45 — External-agent evaluation fixtures

### Goal

Add deterministic evaluation coverage for fake external-agent invocation.

### Add

```text
tests/Agentor.Application.Tests/fixtures/eval/external-agent-one-call.json
RunEvaluationHarness external-agent snapshot support
RunQualityGateEvaluator optional warning for unreviewed external-agent output
```

### Acceptance

- JSON fixture is UTF-8.
- Evaluation snapshot includes external-agent call count or trace signature.
- Quality gate can warn if external-agent output is used without review when configured.
- All tests green.

## Phase 9 exit criteria

- External-agent abstraction exists.
- Fake A2A-style client exists.
- External-agent tools are policy-gated.
- External-agent calls are auditable and evaluable.
- No real protocol transport and no framework ontology leakage.
