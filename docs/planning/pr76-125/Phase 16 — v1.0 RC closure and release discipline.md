# Phase 16 — v1.0 RC closure and release discipline

**PR76–PR80**

Purpose: turn the cleaned RC repository into a controlled release candidate with explicit release artifacts, versioning, and operator verification.

## PR76 — Release manifest and artifact inventory

Add:

```text
docs/RELEASE/v1.0-RC-ARTIFACTS.md
```

Include:

```text
- solution projects
- Docker image name/tag
- EF migration inventory
- CI workflow guarantees
- test project counts
- benchmark compile status
- known deferred rows
- required local verification commands
```

Acceptance:

```text
- Release artifact inventory exists.
- CI-generated artifact expectations documented.
- No false production-readiness claims.
```

## PR77 — Runtime version and build metadata finalization

Add explicit version metadata:

```text
AgentorRuntimeOptions.Version
BuildInfoDto
GET /api/v1/system/version
```

Acceptance:

```text
- API exposes runtime version/build metadata.
- Version defaults to v1.0.0-rc.1 or chosen RC string.
- Tests prove endpoint is deterministic.
```

## PR78 — Environment profiles: local, CI, staging, production template

Add config templates:

```text
appsettings.Local.example.json
appsettings.CI.example.json
appsettings.Staging.example.json
appsettings.Production.example.json
```

Acceptance:

```text
- Production template does not use fake actor silently.
- Fake adapters clearly marked non-production.
- Secrets are represented as placeholders only.
```

## PR79 — Release smoke pack

Add a release smoke workflow/script:

```text
scripts/release-smoke.ps1
docs/operator/release-smoke.md
```

Smoke should cover:

```text
/health
/ready
/api/v1/system/version
/api/v1/integrations/status
basic run creation
manifest retrieval
audit export retrieval
```

Acceptance:

```text
- Script exists.
- Docs explain expected outputs.
- CI may compile/check script, but does not need to run a live deployed environment.
```

## PR80 — v1.0 RC cut checklist

Add:

```text
docs/RELEASE/v1.0-RC-CHECKLIST.md
```

Acceptance:

```text
- Checklist distinguishes RC-ready from production-ready.
- All remaining false rows are classified.
- Harness updated to Phase 16 / PR80.
```
