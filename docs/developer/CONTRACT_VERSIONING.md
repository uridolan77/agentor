# Contract versioning (HTTP JSON)

## Policy

- **Additive-first**: prefer adding optional properties or new DTOs over breaking renames.
- **Breaking changes** require a documented migration window and API version bump when external consumers exist.
- **Wire names**: camelCase JSON (`System.Text.Json` default in API).
- **Enums**: serialize as string names matching `Agentor.Domain` enums; do not rename enum members without a compatibility note.

## Compatibility tests

`tests/Agentor.Contracts.Tests/ContractDtoCompatibilityTests.cs` round-trips representative payloads for shared HTTP contracts.

## Version stamp

Runtime reports `AgentorRuntimeOptions` version (see appsettings). Bump when observable behavior or contract surfaces change incompatibly.
## PR75.6 documentation note

This file is unchanged in substance for PR75.6. RC means **review-ready** boundaries and deferred items are tracked in `docs/RELEASE/v1.0-RC-DEFERRED-ITEMS.md` rather than being silently dropped from the harness.