# Cursor Prompt Series

## Prompt 1 — Compile PR1

```text
You are working on Agentor PR1. Run dotnet restore, dotnet build, and dotnet test. Fix only compile errors, missing references, namespace issues, and test failures. Do not add features. Keep the scope limited to the deterministic AgentRun kernel.
```

## Prompt 2 — Boundary review

```text
Review the code for boundary violations. Agentor must not call Athanor, Conexus, MCP, OpenAI, Anthropic, vector stores, or background jobs in PR1. Remove any accidental runtime dependency that violates docs/SERVICE_BOUNDARIES.md.
```

## Prompt 3 — Domain review

```text
Review Agentor.Domain. Ensure the domain has no infrastructure dependencies, no HTTP concepts, no EF Core attributes, no JSON serialization assumptions, and no external service clients.
```

## Prompt 4 — Application review

```text
Review Agentor.Application. Ensure the handler executes the deterministic PR1 run: create run, create step, evaluate policy, execute fake tool, record policy decision, record tool call, record trace, complete run, save run.
```

## Prompt 5 — API smoke

```text
Run the API and test GET /health, POST /agent-runs, GET /agent-runs/{id}, and GET /agent-runs/{id}/manifest. Fix endpoint mapping or DTO issues only.
```

## Prompt 6 — Prepare PR description

```text
Prepare a PR description for PR1. Include scope, architecture, service boundaries, endpoints, tests, and explicit non-goals.
```
