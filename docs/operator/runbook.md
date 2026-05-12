# Operator runbook (Phase 40 / PR168)

Quick reference for **symptoms → checks → mitigations**. Deep dives live in linked docs.

## Queue appears stuck (items not draining)

**Checks**

- Confirm **`Agentor:RunWorker:Enabled=true`** and the process is running only **one** logical worker deployment if your platform does not coordinate leases externally.
- Inspect **`GET /api/v1/ops/queue`** (requires **OpsRead**) for pending vs claimed items and timestamps.
- Database: verify connectivity, migration level, and lock contention on `run_queue_items`.

**Mitigations**

- Restart worker after fixing config; expired leases should allow reclaim (see lease TTL settings).
- If poison messages exist, mark failed via operational procedures (do not bypass policy without governance).

## Outbox stuck (dispatch not progressing)

**Checks**

- **`Agentor:OutboxDispatch:Enabled`** and sink configuration.
- **`GET /api/v1/ops/outbox`** for retry counts and dispatching flags.
- Logs for dispatcher exceptions (redacted in production patterns).

**Mitigations**

- Fix downstream sink availability; replay or poison-handle per retention policy.
- Never enable **`NoOp`** sink in production without explicit documented exception (see checklist).

## Integration family down (Athanor / Conexus / MCP / external)

**Checks**

- **`GET /api/v1/integrations/status`** (authenticated, **OpsRead**).
- Upstream health, DNS, TLS certificates, and firewall egress.
- **`IntegrationHttpError`-style** logs often include HTTP status and truncated bodies (redacted).

**Mitigations**

- Fail over to healthy upstream endpoints; reduce concurrency temporarily.
- Use **`docs/integrations/compatibility-matrix.md`** for contract expectations.

## Review backlog growing

**Checks**

- Governance inbox endpoints and operator dashboard (see Phase 13 operator docs).
- Policy **`RequiresReview`** density — may indicate mis-tuned bundles.

**Mitigations**

- Add approver capacity; adjust policy bundles only through governed change management.
- Escalation paths require **Jwt** governance approver role (see auth boundary).

## Policy misconfiguration (unexpected denies / reviews)

**Checks**

- Active bundle + profile activation APIs; audit export **`policyIdentity`** sections.
- **`PolicyBundleRulesAdapter`** scope merge rules in `docs/developer/policy-bundles.md`.

**Mitigations**

- Publish corrective bundle version; activate profile binding explicitly; re-run affected plans after human review if runs were paused.

## Auth failures (401 / 403 spikes)

**Checks**

- Auth mode vs probe configuration (`/ready` requires auth).
- Jwt authority reachability, clock skew, audience mismatch.
- Header mode: valid GUID in configured header name.

**Mitigations**

- Align ingress headers or Jwt configuration; see **`docs/security/auth-boundary.md`** and **`AUTHORIZATION_MATRIX.md`**.

## OpenAPI accidentally exposed

**Checks**

- `ASPNETCORE_ENVIRONMENT` and **`Agentor:OpenApi:Enabled`** on production hosts.

**Mitigations**

- Set **`Agentor:OpenApi:Enabled=false`**; confirm with **`OpenApiExposureApiTests`** rationale in security checklist; redeploy.

## Diagnostics capture (for incidents)

- **`GET /api/v1/ops/diagnostics-report`** (`format=json` or `markdown`) with **OpsRead**.
- Correlate with **`X-Ontogony-Trace-Id`** / run trace headers per **`docs/operator/observability.md`**.
- Do **not** paste raw diagnostics into public tickets without reviewing for secrets (redaction helps but is not a substitute for judgment).

## Related scripts

- **`scripts/release-smoke.ps1`** — post-deploy smoke.
- **`scripts/load-smoke.ps1`** — optional load (local/dev; see performance baseline doc).
