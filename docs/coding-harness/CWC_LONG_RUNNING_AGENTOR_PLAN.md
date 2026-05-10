# CWC Long-Running Agent Harness for Agentor

## Source pattern

Anthropic's `cwc-long-running-agents` repo defines three core quality-loop primitives:

1. Default-FAIL contract
2. Fresh-context evaluator
3. Agent-maintained handoff

It also includes two operator-control hooks:

1. Kill switch
2. Steering file

## Agentor adaptation

Agentor should adopt these as a coding-process harness, not as runtime product code.

## Agentor quality loop

```text
PR spec
  ↓
Builder agent implements one PR/pass
  ↓
Builder creates evidence:
  - artifacts/verification/dotnet-restore.txt
  - artifacts/verification/dotnet-build.txt
  - artifacts/verification/dotnet-test.txt
  - artifacts/verification/api-smoke.txt when relevant
  ↓
Builder reads evidence files
  ↓
Builder may update test-results.agentor.json
  ↓
Fresh evaluator reviews diff + evidence
  ↓
PASS → commit
NEEDS_WORK → findings become next builder prompt
```

## Evidence contract

For Agentor, evidence files should live under:

```text
artifacts/verification/
```

Recommended files:

```text
dotnet-info.txt
dotnet-restore.txt
dotnet-build.txt
dotnet-test.txt
api-smoke.txt
postgres-smoke.txt
git-diff-summary.txt
```

The agent must open/read these files before marking any result as passing.

## What counts as evidence

Counts:
- `dotnet build` output
- `dotnet test` output
- API smoke output
- PostgreSQL integration smoke output
- evaluator report

Does not count:
- agent summary
- "looks good"
- unobserved terminal output not captured into a file
- claims without a saved log

## Do not over-automate yet

For Agentor, the harness should initially be advisory/guarded:

- use `PROGRESS.md`
- use evaluator subagent
- use evidence logs
- use kill switch
- use steering file

Delay automatic commits until you trust the flow.
