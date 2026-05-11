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

# Refined Phase 37 — Observability and operator readiness

The current plan is good, but I would make it more concrete and safer. Observability must not become a secret-leak channel.

## PR149 — Observability primitives and structured logs

Instead of only “structured logs,” make this the foundation PR.

Add:

```text
AgentorEventIds
AgentorLogFields
SafeLogContext
ObservabilityRedaction helper if needed
```

Structured log events:

```text
run.started
run.completed
run.failed
run.requires_review

policy.allowed
policy.denied
policy.requires_review

tool.started
tool.completed
tool.failed

queue.claimed
queue.completed
queue.failed

outbox.dispatch.started
outbox.dispatch.completed
outbox.dispatch.failed

integration.error
```

Rules:

```text
- No ToolPayload.Body
- No raw prompts
- No raw external response bodies
- No authorization headers
- No tokens
- IDs, status, counts, durations, tool keys, policy effect are allowed
```

Acceptance:

```text
- Unit tests or log-capture tests prove no payload body is logged.
- Logs include runId where available.
- Integration errors use redacted/truncated text.
```

## PR150 — Metrics surface

Use `.NET Meter` / `System.Diagnostics.Metrics` first, not a custom database model.

Counters/gauges:

```text
agentor.runs.started
agentor.runs.completed
agentor.runs.failed
agentor.runs.requires_review
agentor.policy.allowed
agentor.policy.denied
agentor.policy.requires_review
agentor.tools.started
agentor.tools.completed
agentor.tools.failed
agentor.queue.depth
agentor.outbox.pending
agentor.integrations.errors
```

Acceptance:

```text
- Metrics are emitted with safe dimensions only.
- Dimensions: toolKey, policyEffect, integrationName, status.
- No user payloads or raw objectives as tags.
```

## PR151 — Trace correlation

Add a correlation policy:

```text
- Every API response gets X-Agentor-Trace-Id.
- Every request has a correlation id in logs.
- Existing run TraceId is linked to request trace id where possible.
- Integration HTTP errors include correlation id where available.
```

Acceptance:

```text
- API tests prove X-Agentor-Trace-Id exists.
- Run-created response can be correlated with logs/trace id.
- Integration error handling preserves correlation metadata without leaking secrets.
```

## PR152 — Operator diagnostics bundle

Generate:

```text
diagnostics-report.json
diagnostics-report.md
```

Include safe summaries:

```text
runtime version
environment
auth mode summary
OpenAPI exposure status
integration readiness
queue depth
outbox pending
recent failed runs count
review backlog count
policy profile/bundle status
evaluation artifact presence
migration/provider info
```

Do not include:

```text
ToolPayload.Body
raw audit packet contents
raw exception stack traces
tokens
connection strings
headers
full upstream error bodies
```

Acceptance:

```text
- Diagnostics report is redacted.
- JSON and Markdown outputs are deterministic enough for tests.
- Operator doc explains proof boundaries.
```

## PR153 — Observability docs

Add:

```text
docs/operator/observability.md
```

Cover:

```text
logs
metrics
trace correlation
diagnostics bundle
safe fields
forbidden fields
how to use diagnostics during incidents
```

---

# Refined Phase 38 — Security hardening final pass

This phase should depend on Phase 37 because it must audit logs and diagnostics too.

## PR154 — Secret leak audit

Scan and test:

```text
audit export
timeline
operator dashboard
diagnostics report
structured logs
evaluation reports
integration smoke reports
release smoke reports if added
queue/outbox errors
integration error messages
```

Acceptance:

```text
- Redaction tests include nested JSON, summaries, diagnostics, logs.
- Known secret keys are consistently redacted.
```

## PR155 — Permission matrix tests

This is important but can get large. Do not test every route manually in one giant file. Add a table-driven route matrix.

Roles:

```text
unauthenticated
Service
HumanOperator
HumanGovernanceApprover
System
```

Acceptance:

```text
- Every protected route in AUTHORIZATION_MATRIX.md has a matching test row.
- Docs and tests cannot drift silently.
```

## PR156 — Threat-model update

Update:

```text
docs/security/deployment-threat-notes.md
docs/security/auth-boundary.md
docs/security/SECURITY_RELEASE_CHECKLIST.md
```

Include:

```text
trusted ingress assumptions
Header auth risks
Jwt unvalidated-token escape hatch
Fake auth production block
OpenAPI exposure
integration smoke write gate
diagnostics/logging leak risks
queue/outbox operational risks
```

## PR157 — Safe defaults audit

Verify production defaults:

```text
Fake auth blocked
OpenAPI disabled
unvalidated JWT blocked
NoOp outbox sink blocked
workers disabled unless configured
Athanor write smoke disabled
integration smoke disabled
diagnostics contains no secrets
```

## PR158 — Security review report

Add:

```text
docs/security/v1-security-review.md
```

This should be honest: evidence, boundaries, remaining risks, and production assumptions.

---

# Refined Phase 39 — Performance and stress baseline

Good as written, but keep it non-marketing and local-first.

## PR159 — Benchmark suite update

Benchmark:

```text
single-tool run
plan run
policy evaluation
audit export
timeline generation
queue claim
EF save
diagnostics report generation
```

## PR160 — Load smoke

Local script:

```text
scripts/load-smoke.ps1
```

Config:

```text
-runCount
-queueCount
-concurrency
-workload fake|review|required|mixed
-outputDirectory
```

## PR161 — Persistence stress

Tests:

```text
many traces
many tool calls
many policy decisions
large resume cursor
large audit export
queue/outbox volume
```

## PR162 — Evaluation performance report

Connect performance outputs to Phase 32 reporting:

```text
performance-report.json
performance-report.md
performance-summary.csv
```

## PR163 — Performance docs

Add:

```text
docs/developer/performance-baseline.md
```

Tone:

```text
measured on local/dev hardware
not production SLOs
not scalability claims
```

---

# Refined Phase 40 — v1 release closure

This is the correct final discipline phase.

## PR164 — Final deferred-item audit

Acceptance:

```text
passes:false = 0
or explicit accepted release deferrals
```

Also scan:

```text
TODO
FIXME
NotImplementedException
NotSupportedException
unsupported
future work
```

## PR165 — Versioning and changelog

Add:

```text
CHANGELOG.md
docs/RELEASE/v1.0-RC-TAGGING.md
version endpoint final value
```

## PR166 — Deployment guide

Add:

```text
docs/deployment/local.md
docs/deployment/staging.md
docs/deployment/production.md
```

Must include:

```text
PostgreSQL configuration
auth mode selection
OpenAPI gating
workers/outbox enablement
integration endpoint configuration
secret management
```

## PR167 — Backup / restore / migration guide

Extend the current migration doc into an operator guide:

```text
backup before migration
restore procedure
rollback application image
queue/outbox considerations
migration verification
```

## PR168 — Operator runbook

Add:

```text
docs/operator/runbook.md
```

Cases:

```text
queue stuck
outbox stuck
integration down
review backlog
policy misconfiguration
auth failures
OpenAPI accidentally exposed
diagnostics capture
```

## PR169 — Final RC verification

Full commands:

```text
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
verify-harness
verify-repo-clean
dotnet ef migrations list
docker build
release smoke
evaluation report
security checklist
diagnostics report
benchmark compile
```

## PR170 — v1.0 release candidate declaration

Acceptance:

```text
phase: 40
harnessPass: PR170
release candidate declared
```

Add final doc:

```text
docs/RELEASE/v1.0-RC-FINAL.md
```

---

# Important change to the planning file

The current `Phase 32 - 40.md` should now be split or updated because Phases 32–36 are done. Keep history, but add a new active section:

```text
# Active remaining roadmap after Phase 36

- PR148.5 — RC closeout polish
- Phase 37 — Observability and operator readiness
- Phase 38 — Security hardening final pass
- Phase 39 — Performance and stress baseline
- Phase 40 — v1 release closure
```

Do not leave the stale “highest-value sequence” that starts with PR122.5 / Phase 32. That was correct earlier, but now it is misleading.

---

# Immediate next prompt

Use this after PR148.5, or include the planning-doc cleanup inside PR148.5.

```text
We are implementing PR148.5 — RC closeout polish and active roadmap cleanup.

This is a small second-pass correction after Phase 36. Do not start Phase 37.

Current known state:
- Phase 36 PR143–PR148 is harness-accepted.
- Current harness is phase 36 / PR148 with 530 tests passing.
- Active deferred harness rows = 0.
- Phase 37 observability has not started.
- Phase 32–36 are already complete, but docs/planning/pr76-125/Phase 32 - 40.md still contains stale “next priority” wording that starts from PR122.5 / Phase 32.

Implement only:
1. Harden Agentor.IntegrationSmoke CLI parsing:
   - --target / -t with no value fails with exit code 2.
   - --output / -o with no value fails with exit code 2.
   - unknown flags fail with exit code 2.
   - add tests if practical.

2. Clean stale migration doc note:
   - remove or move the old PR75.6 note from docs/developer/MIGRATION_AND_UPGRADE.md.
   - preserve migration inventory and provider support boundaries.

3. Update active roadmap section:
   - In docs/planning/pr76-125/Phase 32 - 40.md, keep historical Phase 32–36 content but mark it completed, or add a clear “Active remaining roadmap after Phase 36” section.
   - Remove or supersede stale priority text that says the next steps are PR122.5 / Phase 32.
   - Active next sequence should be:
     - Phase 37 Observability
     - Phase 38 Security hardening
     - Phase 39 Performance/stress
     - Phase 40 v1 release closure

4. Optional release smoke report:
   - If low-risk, add -OutputDirectory and write release-smoke-report.json / .md.
   - If not, document as Phase 37 operator artifact work.

5. Update harness:
   - current-pr.md: Phase 36 PR143–PR148 + PR148.5 complete.
   - feature-list.json: phase 36, harnessPass PR148.5.
   - progress.md, verification-log.md, session-handoff.md.
   - passes:false remains 0.

Do not add:
- structured logging
- metrics
- trace propagation
- diagnostics bundle
- new runtime behavior
- new auth modes
- new integrations

Run:
- dotnet restore Agentor.sln
- dotnet build Agentor.sln --no-restore
- dotnet test Agentor.sln --no-build
- pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-harness.ps1 -ExpectedPhase 36 -ExpectedHarnessPass PR148.5
- pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/verify-repo-clean.ps1

Final report:
1. Files changed.
2. Integration smoke CLI validation behavior.
3. Migration doc cleanup.
4. Active roadmap cleanup.
5. Release smoke report decision.
6. Final test count.
7. verify-harness result.
8. verify-repo-clean result.
9. Remaining passes:false items.
10. Confirmation Phase 37 was not started.
```
