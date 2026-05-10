# Phase 1 — Runtime kernel and API hardening

## PRs

- PR01 — Runtime foundation stabilization
- PR02 — API contract hardening and DTO versioning
- PR03 — Run manifest determinism and trace hash
- PR04 — Validation, error model, and request tracing hardening
- PR05 — Configuration/options model and startup validation

## Phase-end audit

After this phase, ask Claude Code:

```text
Perform a phase boundary audit. Check CWC decomposition, Athanor/Conexus/MCP boundaries, external framework adapter strategy, missing tests, and scope creep. Do not edit files until I approve a remediation plan.
```
