# PR35 — Run quality gates and evaluation summaries

## Objective

Add quality gates that pass/fail runs based on evaluation and policy/manifest properties.

## Related architecture (PR12.5)

Quality gates should eventually reflect **coordination evaluation signatures** (reliability/calibration, resolution, cost/latency, token/compute, diversity collapse, escalation, failure isolation, termination quality) in addition to single-score success. Keep gates explainable from manifest and trace evidence; do not conflate Athanor authority or Conexus routing with coordination outcomes.

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
Implement PR35 — Run quality gates and evaluation summaries.

Read:
- CLAUDE.md
- AGENTS.md
- docs/CWC_WORKSHOP_LESSONS_APPLIED.md
- docs/FRAMEWORK_STRATEGY.md
- docs/SERVICE_BOUNDARIES.md
- docs/planning/pr1-pr40/prs/PR35-quality-gates-evaluation-summary.md

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
