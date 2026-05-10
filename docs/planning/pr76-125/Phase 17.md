
---

# Phase 17 — Enterprise policy model

**PR81–PR85**

Purpose: replace the current partial `PolicyProfileRules` model with a versioned, auditable, composable policy system.

## PR81 — PolicyBundle domain model

Add:

```text
PolicyBundle
PolicyBundleId
PolicyBundleVersion
PolicyRule
PolicyRuleKind
PolicyRuleScope
PolicyRuleEffect
```

Rules:

```text
- Domain model only.
- No runtime evaluator rewrite yet.
- Versioned and immutable after publication.
```

Acceptance:

```text
- PolicyBundle validates identity/version/rules.
- Duplicate rule IDs rejected.
- Tests cover rule construction and validation.
```

## PR82 — PolicyProfile binding to bundle versions

Add:

```text
PolicyProfile
PolicyProfileBinding
ActivePolicyProfile
```

Acceptance:

```text
- Profile can bind to a specific PolicyBundle version.
- Existing RuntimePolicyOptions still works as compatibility path.
- Tests prove fallback path remains.
```

## PR83 — Policy evaluation adapter

Bridge bundle rules into the existing `RuntimePolicyEvaluator`.

Acceptance:

```text
- Tool risk rules work.
- Model budget rules work.
- MCP tool deny rules work.
- External-agent deny rules work.
- RequiresReview remains distinct from Deny.
```

## PR84 — Policy management API

Add endpoints:

```text
GET /api/v1/policy-bundles
POST /api/v1/policy-bundles
GET /api/v1/policy-bundles/{id}
POST /api/v1/policy-profiles/{id}/activate
```

Acceptance:

```text
- Creating a policy bundle does not activate it automatically.
- Activation is explicit and audited.
- Existing policy-profile artifact endpoints remain compatible.
```

## PR85 — Policy audit and evaluation fixtures

Add deterministic policy fixtures:

```text
tests/.../fixtures/policy/
docs/developer/policy-bundles.md
```

Acceptance:

```text
- Evaluation fixture proves bundle-driven allow/deny/review.
- Audit export includes active policy profile/bundle identity.
- PR52-004 becomes true only if all evidence exists.
```
