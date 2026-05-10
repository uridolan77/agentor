# Phase 15 — v1.0 platform hardening

## Purpose

Turn the post-v0.1 platform into a credible v1.0 release candidate.

## Doctrine

```text
v1.0 requires boundary review, security hygiene, migration readiness, performance baselines, documentation, and deterministic release artifacts.
False harness items must be closed or explicitly deferred with justification.
```

## PR71 — Security and secret hygiene audit

### Goal

Protect logs, traces, manifests, audit packets, reports, and exports from secret leakage.

### Add

```text
RedactionPolicy
SensitiveFieldCatalog
RedactionResult
```

### Acceptance

- Secrets are not emitted in traces/manifests/audit exports/logs.
- Redaction tests cover configured sensitive fields.
- Docs explain what is redacted and why.

## PR72 — Performance and load baseline

### Goal

Add performance baselines before v1.0.

### Targets

```text
Run creation
Plan execution
Manifest generation
Timeline query
Audit packet export
Evaluation fixture run
```

### Acceptance

- Benchmark or load smoke tests exist.
- Baseline numbers are documented.
- Regression thresholds are defined.

## PR73 — CI/CD release pipeline v1

### Goal

Harden release automation.

### Pipeline includes

```text
restore
build
test
migration check
Docker build
evaluation fixture regression
security/redaction tests
release artifact generation
```

### Acceptance

- GitHub Actions pipeline is deterministic.
- CI artifacts include evaluation reports.
- Docker image build is verified.

## PR74 — Upgrade and migration readiness

### Goal

Make upgrades safe.

### Acceptance

- Migration checklist exists.
- DTO compatibility tests exist.
- Contract versioning policy exists.
- Rollback considerations documented.

## PR75 — v1.0 release candidate

### Goal

Produce v1.0 RC readiness packet.

### Acceptance

- All critical tests green.
- All harness false items are either closed or explicitly deferred.
- Docs updated.
- Release notes written.
- Architecture-boundary review passes:

```text
Agentor coordinates.
Athanor canonizes.
Conexus routes models.
MCP/external frameworks are adapters.
Session memory is scratch.
External-agent output is non-canon unless explicitly submitted to Athanor as candidate/review material.
```

## Phase 15 exit criteria

- Security hygiene passes.
- Performance baseline exists.
- Release pipeline exists.
- Migration readiness exists.
- v1.0 RC notes and boundary review exist.
