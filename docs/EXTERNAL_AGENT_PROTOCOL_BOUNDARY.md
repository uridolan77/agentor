# External agent protocol boundary

## Role

External-agent protocols (including A2A-shaped transports in future phases) are **adapters** behind `IExternalAgentProtocolClient`. They are not part of Agentor domain ontology.

## Rules

- Domain and Application **must not** reference protocol SDK types, HTTP clients, or framework-specific agent graphs.
- Contract DTOs under `Agentor.Contracts.ExternalAgents` describe portable payloads only.
- Outputs from external agents are **non-canon evidence** until reviewed in Athanor or another authority outside Agentor.
- Phase 9 ships **fake, deterministic** implementations only (no real network transport).

## Port

`IExternalAgentProtocolClient` (`Agentor.Application.Abstractions`):

- `ListCapabilitiesAsync` — discovery-shaped capability rows for policy-gated tooling.
- `InvokeAsync` — invocation-shaped call with dictionary inputs matching other Agentor tools.

Concrete adapters live in Infrastructure and register through dependency injection.
