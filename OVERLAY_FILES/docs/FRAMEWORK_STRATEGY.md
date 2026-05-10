# Framework Strategy

## Decision

Agentor is framework-compatible, not framework-dependent.

External frameworks may be integrated as adapters only after Agentor's runtime primitives are stable.

## Core primitives

Agentor's internal primitives remain:

```text
AgentRun
AgentPlan
AgentStep
ToolCall
PolicyDecision
ExecutionTraceEvent
RunManifest
SkillInvocation
EvaluationResult
```

## Adapter candidates

Possible adapters:

```text
Agentor.Mcp.Adapter
Agentor.SemanticKernel.Adapter
Agentor.MicrosoftAgent.Adapter
Agentor.A2A.Adapter
Agentor.LangGraph.Adapter
Agentor.AutoGen.Adapter
```

## Microsoft Agent Framework

Use later only if it helps with:
- remote agent hosting
- enterprise agent integration
- .NET-native agent infrastructure
- Azure/Foundry deployment

Do not use it to define Agentor's core runtime.

## Semantic Kernel

Use later only if it helps with:
- prompt templates
- function wrappers
- connector abstractions
- prompt rendering

Model calls should still route through Conexus.

## MCP

Use later for:
- tool discovery
- tool invocation protocol
- external tool capability binding

MCP tools must enter through Agentor ToolRegistry and PolicyEvaluator.

## A2A

Use later for:
- external agent communication
- remote agent invocation
- cross-agent trace correlation

A2A should be post-v0.1 unless required earlier.
