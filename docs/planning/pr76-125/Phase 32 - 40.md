# Phase 32 — Evaluation science v2

**PR123–PR127**

Purpose: make Agentor measurable beyond “tests pass.” The existing planning doc says this phase should move evaluation from deterministic regression into comparative coordination analysis: datasets, baselines, deltas, aggregate metrics, thresholds, and CI artifacts. 

## PR123 — Evaluation dataset registry

Add:

```text
EvaluationDataset
EvaluationCase
EvaluationCaseTag
EvaluationDatasetRegistry
```

Acceptance:

```text
- JSON dataset registry loads deterministically.
- Duplicate dataset/case IDs fail validation.
- Missing fixture references fail validation.
- Tags can select subsets: smoke, regression, review, external-agent, policy, queue.
```

## PR124 — Baseline and delta model

Add:

```text
EvaluationBaseline
EvaluationDelta
EvaluationDeltaKind
```

Compare:

```text
- status changes
- quality violations
- warnings
- trace event counts
- review burden
- latency
- cost
- policy decisions
- external-agent invocation count
```

Acceptance:

```text
- Current snapshot can compare against stored baseline.
- Deltas serialize to stable JSON.
- Tests cover improvement, regression, and neutral delta.
```

## PR125 — Aggregate evaluation reports

Add:

```text
EvaluationAggregateReport
EvaluationAggregateReportGenerator
```

Metrics:

```text
mean latency
median latency
failure rate
review burden
policy deny rate
requires-review rate
external-agent usage
cost distribution
quality violation distribution
```

Acceptance:

```text
- Deterministic report output.
- Markdown + JSON report generation.
- CSV summary if low-risk.
```

## PR126 — Regression thresholds

Add:

```text
evaluation-thresholds.json
EvaluationThresholdEvaluator
```

Support:

```text
pass
warn
fail
```

Example thresholds:

```text
maxFailureRateIncrease
maxReviewBurdenIncrease
maxLatencyIncreasePercent
maxCostIncreasePercent
maxQualityViolationIncrease
```

Acceptance:

```text
- Threshold pass/warn/fail tests.
- Regression reason codes are stable.
```

## PR127 — CI evaluation artifacts

Add CI artifacts:

```text
evaluation-report.md
evaluation-report.json
evaluation-summary.csv
```

Acceptance:

```text
- CI can publish evaluation artifacts.
- If CI upload is risky, add scripts and document the CI follow-up.
- Harness phase 32 / PR127.
```

---

# Phase 33 — Structured queue payload upgrade

**PR128–PR132**

Purpose: close the mismatch between structured `ToolPayload` runtime execution and queue persistence.

Repo truth currently says queue rows persist selectors and `tool_input_json` for **string-keyed tool inputs**.  After Phase 30, execution uses structured `ToolPayload`, so the queue should also persist full structured payloads.

## PR128 — Queue payload model

Add:

```text
RunQueuePayload
RunQueuePayloadVersion
ToolPayload queue serialization
```

Acceptance:

```text
- Legacy string-keyed queue input still loads.
- New ToolPayload queue input persists as v2 envelope.
```

## PR129 — EF queue payload migration

Add EF migration:

```text
tool_payload_json
tool_input_json legacy preserved
```

Acceptance:

```text
- Existing rows remain readable.
- New rows write ToolPayload.
- Tests cover legacy and v2 queue rows.
```

## PR130 — API → queue structured payload path

Ensure `POST /agent-runs/queue` or equivalent queue-start path preserves:

```text
mode
toolKey
planId
recipeId
skillKey
ToolPayload body
schemaId
contentType
summary
scope fields
```

Acceptance:

```text
- Queued Conexus run receives structured payload.
- Queued MCP run receives structured payload.
- Queued external-agent run receives structured payload.
```

## PR131 — Worker replay parity

Acceptance:

```text
- Inline public run and queued run produce equivalent tool input shape.
- Policy evaluation receives the expected scalar projection.
- Audit export shows structured body/summary after queued execution.
```

## PR132 — Queue payload docs and fixtures

Add:

```text
docs/operator/queue-payloads.md
fixtures/eval/queued-structured-toolpayload.json
```

---

# Phase 34 — Skill resume support

**PR133–PR137**

Purpose: remove the main resume limitation.

`PlanResumeOrchestrator` currently has an explicit unsupported path for skill steps during resume. It records `SKILL_RESUME_NOT_SUPPORTED` and applies failure policy. 

## PR133 — Skill resume design

Define:

```text
SkillResumeCursor
SkillProcedureResumeState
SkillInnerToolCheckpoint
```

Acceptance:

```text
- Design explains skill segment resume.
- Approval never grants forward license.
- Skill resume remains non-canon.
```

## PR134 — Resume skill procedure from checkpoint

Acceptance:

```text
- Skill procedure can resume after blocked inner tool.
- Remaining procedure steps execute in order.
- Skill traces preserve procedureStepId.
```

## PR135 — Skill step inside plan resume

Acceptance:

```text
- Plan resume can continue into a skill step.
- Skill step can itself require review.
- RequiresReview chaining records correct cursor.
```

## PR136 — Skill resume failure policies

Cover:

```text
FailFast
ContinueOnFailure
SkipRemaining
MarkForCompensation
EscalateToReview
```

## PR137 — Skill resume fixtures and audit

Acceptance:

```text
- Evaluation fixture for plan → skill → review → resume.
- Audit export shows skill procedure resume path.
- No hidden canonization.
```

---

# Phase 35 — Production integration smoke pack

**PR138–PR142**

Purpose: prove configuration-level real integration behavior without hardcoding external services.

The repo has HTTP adapters and contract tests, but production readiness still needs smoke scripts and operator proof.

## PR138 — Integration smoke configuration model

Add:

```text
IntegrationSmokeOptions
SmokeTarget
SmokeMode = Disabled | Fake | Http
```

Acceptance:

```text
- Disabled by default.
- No secrets in repo.
- Env-var driven.
```

## PR139 — Athanor smoke

Smoke:

```text
latest snapshot
canonical lookup
evidence search
candidate submit disabled unless explicit write smoke enabled
```

Acceptance:

```text
- Read smoke safe by default.
- Write smoke gated.
```

## PR140 — Conexus smoke

Smoke:

```text
model completion
declared budget pass-through
telemetry received
```

## PR141 — MCP / external-agent smoke

Smoke:

```text
MCP server list
MCP tool discovery
MCP tool invoke
external-agent discover
external-agent invoke
```

## PR142 — Operator smoke report

Generate:

```text
integration-smoke-report.md
integration-smoke-report.json
```

Acceptance:

```text
- Redacted.
- Status-code-bearing errors.
- CI optional; local/operator script required.
```

---

# Phase 36 — Release candidate consolidation

**PR143–PR148**

Purpose: stop feature expansion and harden the release package.

## PR143 — Repo truth reconciliation

Re-read and update:

```text
README.md
docs/REPO_TRUTH.md
docs/RELEASE/*
docs/security/*
docs/operator/*
```

Acceptance:

```text
- No stale phase numbering.
- No old “Phase 23 / PR111” mismatch in Phase 32 docs.
- No overclaiming production readiness.
```

## PR144 — Migration audit

Acceptance:

```text
- All EF migrations compile.
- Migration list documented.
- PostgreSQL / SQL Server / SQLite support boundaries stated.
```

## PR145 — API contract snapshot

Generate:

```text
openapi snapshot
route-permission matrix
DTO compatibility report
```

Acceptance:

```text
- Public API changes are explicit.
- OpenAPI gating documented.
```

## PR146 — Release smoke script

Smoke:

```text
/health
/ready
/api/v1/integrations/status
POST /agent-runs
GET /agent-runs/{id}
GET trace
GET audit export
GET operator dashboard
```

## PR147 — Security checklist

Acceptance:

```text
- Auth modes documented.
- JWT unvalidated override documented as dangerous.
- Header mode limitations documented.
- Secrets redaction tests pass.
- OpenAPI exposure posture confirmed.
```

## PR148 — RC harness reconciliation

Acceptance:

```text
phase: 36
harnessPass: PR148
passes:false = 0 or explicitly accepted release deferrals
```

---

# Phase 37 — Observability and operator readiness

**PR149–PR153**

Purpose: make the system diagnosable.

## PR149 — Structured logs

Add safe structured log events for:

```text
run started/completed/failed
policy allow/deny/review
tool started/completed/failed
queue claim/complete/fail
outbox dispatch
integration errors
```

No payload bodies.

## PR150 — Metrics surface

Add counters/gauges:

```text
run count
failed count
requires-review count
queue depth
outbox pending
policy denies
integration failures
```

## PR151 — Trace correlation

Acceptance:

```text
- Every API response has trace id.
- Run trace id correlates with logs.
- Integration errors carry trace id where possible.
```

## PR152 — Operator diagnostics bundle

Generate:

```text
diagnostics-report.json
diagnostics-report.md
```

## PR153 — Observability docs

Add:

```text
docs/operator/observability.md
```

---

# Phase 38 — Security hardening final pass

**PR154–PR158**

Purpose: one dedicated adversarial review pass.

## PR154 — Secret leak audit

Scan and test:

```text
audit export
timeline
operator dashboard
logs
evaluation reports
integration smoke reports
queue/outbox errors
```

## PR155 — Permission matrix tests

Acceptance:

```text
Every protected route has explicit tests for:
- unauthenticated
- Service
- HumanOperator
- HumanGovernanceApprover
- System
```

## PR156 — Threat-model update

Update:

```text
docs/security/deployment-threat-notes.md
docs/security/auth-boundary.md
```

## PR157 — Safe defaults audit

Verify production defaults:

```text
Fake auth blocked
OpenAPI disabled
unvalidated JWT blocked
NoOp outbox sink blocked
workers disabled unless configured
write smoke disabled
```

## PR158 — Security review report

Add:

```text
docs/security/v1-security-review.md
```

---

# Phase 39 — Performance and stress baseline

**PR159–PR163**

Purpose: know the runtime limits before release.

## PR159 — Benchmark suite update

Bench:

```text
single-tool run
plan run
policy evaluation
audit export
timeline
queue claim
EF save
```

## PR160 — Load smoke

Local script:

```text
N runs
M queue items
review-required workload
mixed tool workload
```

## PR161 — Persistence stress

Test:

```text
many traces
many tool calls
many policy decisions
resume cursor persistence
```

## PR162 — Evaluation performance report

Tie benchmark deltas into Phase 32 reports.

## PR163 — Performance docs

State realistic limits, not marketing claims.

---

# Phase 40 — v1 release closure

**PR164–PR170**

Purpose: final release discipline.

## PR164 — Final deferred-item audit

Acceptance:

```text
passes:false = 0
or release deferrals explicitly accepted
```

## PR165 — Versioning and changelog

Add:

```text
CHANGELOG.md
version endpoint final value
release tag docs
```

## PR166 — Deployment guide

Add:

```text
docs/deployment/local.md
docs/deployment/staging.md
docs/deployment/production.md
```

## PR167 — Backup / restore / migration guide

Especially for EF database.

## PR168 — Operator runbook

Add:

```text
incident triage
queue stuck
outbox stuck
integration down
review backlog
policy misconfiguration
```

## PR169 — Final RC verification

Full commands:

```text
restore
build
test
verify-harness
verify-repo-clean
migration list
docker build
release smoke
evaluation report
security checklist
```

## PR170 — v1.0 release candidate

Acceptance:

```text
phase: 40
harnessPass: PR170
release candidate declared
```

---

# Priority order

The highest-value sequence is:

```text
1. PR122.5 — close Phase 31 properly
2. Phase 32 — evaluation science v2
3. Phase 33 — structured queue payload
4. Phase 34 — skill resume support
5. Phase 35 — production integration smoke
6. Phase 36 — release candidate consolidation
```

The most dangerous gaps are:

```text
- Skill resume unsupported
- Queue payload not fully ToolPayload-native
- Real integrations not smoke-proven
- Evaluation still not comparative enough
- Release docs likely to drift unless consolidated
```

# Immediate next prompt after PR122.5

```text id="ei9e73"
We are starting Phase 32 — Evaluation science v2.

Current known state:
- PR122.5 is complete and harness-accepted.
- Phase 31 is closed.
- passes:false is 0.
- Existing evaluation supports deterministic fixtures, quality gates, coordination metrics, and reports, but not full comparative evaluation.
- Phase 32 planning exists but has stale internal numbering that must be corrected.
- Phase 33 structured queue payload work must not start.

Implement Phase 32 only:
1. PR123 — Evaluation dataset registry.
2. PR124 — Baseline and delta model.
3. PR125 — Aggregate reports.
4. PR126 — Regression thresholds.
5. PR127 — CI/artifact publishing.

Do not add:
- structured queue payload changes
- skill resume support
- production integration smoke
- new auth behavior
- new execution semantics

Run:
- dotnet restore Agentor.sln
- dotnet build Agentor.sln --no-restore
- dotnet test Agentor.sln --no-build
- pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-harness.ps1 -ExpectedPhase 32 -ExpectedHarnessPass PR127
- pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-repo-clean.ps1

Final report:
1. Files changed.
2. Dataset registry behavior.
3. Baseline/delta behavior.
4. Aggregate report behavior.
5. Threshold behavior.
6. CI/artifact behavior.
7. Planning-doc numbering fix.
8. Tests added/updated and final count.
9. verify-harness result.
10. verify-repo-clean result.
11. Remaining passes:false items.
12. Confirmation Phase 33 was not started.
```
