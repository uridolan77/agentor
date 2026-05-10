
---

# Phase 24 — Security hardening v2

**PR116–PR120**

Purpose: move beyond key-name redaction and basic auth boundaries.

## PR116 — Redaction policy v2

Add path-based and type-based redaction:

```text
JsonPointer patterns
allowlist/denylist mode
structured redaction reports
```

## PR117 — Secret scanning test fixtures

Add fixtures proving secrets do not appear in:

```text
traces
audit exports
logs
evaluation reports
integration status
```

## PR118 — Request/response logging policy

Define safe logging rules.

## PR119 — Security headers and deployment defaults

Add minimal secure defaults for production profile.

## PR120 — Security review report

Add:

```text
docs/security/v1.1-security-review.md
```