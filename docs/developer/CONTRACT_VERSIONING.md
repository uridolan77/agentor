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