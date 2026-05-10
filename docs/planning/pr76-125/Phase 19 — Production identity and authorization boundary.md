
---

# Phase 19 — Production identity and authorization boundary

**PR91–PR95**

Purpose: move from header/fake actor into a real deployable auth boundary, without hardcoding a specific identity provider.

## PR91 — Auth mode configuration

Add:

```text
Agentor:Auth:Mode = Fake | Header | Jwt
```

Acceptance:

```text
- Fake mode allowed only in local/test by default.
- Header mode explicit.
- Jwt mode configured but provider-neutral.
```

## PR92 — Actor roles and authorization policies

Add:

```text
ActorRole
AgentorPermission
AuthorizationDecision
```

Acceptance:

```text
- Mutating governance endpoints require permission.
- Read-only endpoints allow read permission.
- Tests cover deny/allow.
```

## PR93 — JWT actor accessor

Add infrastructure JWT accessor behind existing actor boundary.

Acceptance:

```text
- No provider-specific SDK required.
- Claims mapping configurable.
- Tests use fake JWT/claims principal.
```

## PR94 — API authorization middleware / endpoint filters

Apply authorization consistently.

Acceptance:

```text
- Policy bundle activation requires governance permission.
- Review decisions require reviewer permission.
- Audit export requires audit/read permission.
```

## PR95 — Auth/security documentation and threat notes

Add:

```text
docs/security/auth-boundary.md
docs/security/deployment-threat-notes.md
```

Acceptance:

```text
- Fake/header modes clearly non-production unless explicitly accepted.
- JWT configuration documented.
- No secrets in docs.
```
