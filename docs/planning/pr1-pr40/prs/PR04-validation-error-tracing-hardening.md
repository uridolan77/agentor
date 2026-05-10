# PR04 — Validation, error model, and request tracing hardening

## Objective

Standardize validation, machine-readable errors, and trace IDs across success and failure responses.

## Medium-long pass scope

This PR should implement one coherent runtime layer with code, tests, and documentation updates. It should not be split into tiny cosmetic PRs, but it must not implement the next phase early.

## CWC lesson applied

This PR should strengthen Agentor's decomposed runtime model. Keep tools, skills, memory, evals, traces, policies, and adapters conceptually separate.

## Framework rule

External frameworks are adapters, not core. A2A and external agent frameworks are non-goals for this PR unless the title explicitly says otherwise.

## Service boundary rule

External services must remain absent unless this PR explicitly introduces their boundary.

## Required implementation steps

1. Inspect current repo state and existing tests.
2. Identify the narrow set of Domain/Application/Infrastructure/Api files required.
3. Add or update tests before broad refactoring.
4. Implement the smallest coherent design for this PR.
5. Update docs if behavior or boundaries changed.
6. Run build and tests.

## Non-goals

- Do not adopt Microsoft Agent Framework as core.
- Do not adopt Semantic Kernel as core.
- Do not introduce A2A before post-v0.1 planning.
- Do not allow Agentor to canonize knowledge.
- Do not call model providers directly.
- Do not bypass ToolRegistry or PolicyEvaluator for executable actions.

## Claude Code prompt

```text
Implement PR04 — Validation, error model, and request tracing hardening.

Read:
- CLAUDE.md
- AGENTS.md
- docs/CWC_WORKSHOP_LESSONS_APPLIED.md
- docs/FRAMEWORK_STRATEGY.md
- docs/SERVICE_BOUNDARIES.md
- docs/planning/pr1-pr40/prs/PR04-validation-error-tracing-hardening.md

Do not edit files until you summarize scope, non-goals, files, tests, and boundary risks.

Then implement only this PR. Run dotnet restore, dotnet build, and dotnet test.
```

## Definition of done

- [ ] Build succeeds.
- [ ] Tests pass.
- [ ] Docs updated where needed.
- [ ] No service-boundary violation.
- [ ] No external framework leaked into Domain.
- [ ] Final summary names changed files and tests.
