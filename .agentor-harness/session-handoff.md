# Session handoff - Agentor harness

## Done (PR30.5 — hardening after Phase 6)

- Model-call telemetry aggregation moved out of Domain: RunManifest.FromRun(run, RunManifestModelTelemetry); ModelCallTelemetryAggregator in Application parses successful conexus.model-complete tool outputs.
- Declared budget semantics documented as optional pre-execution checks (declaredCostUnits, declaredLatencyMs vs caps); tests when keys omitted with caps configured.
- Harness feature-list: PR30.5-001 … PR30.5-004.

## Done (Phase 6 — PR26–PR30)

- Conexus port (IModelGatewayClient), Contracts gateway DTOs, FakeModelGatewayClient, DI registration.
- Tool conexus.model-complete via ModelGatewayToolExecutor (policy-governed; routes through gateway only).
- Optional promptProfileRef / modelProfileRef on calls and echoed in tool outputs for manifests.
- Declared budget gates: optional tool inputs declaredCostUnits, declaredLatencyMs vs RuntimePolicyOptions caps.
- Run manifest v1.1 aggregates successful Conexus model-call tool outputs into RunManifest / RunManifestDto.

Harness files updated: progress.md, verification-log.md, feature-list.json, session-handoff.md.

## Next

- PR31 Skill package model (Phase 7) per docs/planning/pr1-pr40/PR_INDEX.md.

## Read first

- .agentor-harness/feature-list.json
- docs/CONEXUS_INTEGRATION_BOUNDARY.md