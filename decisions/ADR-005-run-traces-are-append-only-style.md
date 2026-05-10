# ADR-005 — Run Traces Are Append-Only Style

## Status

Accepted

## Decision

Execution traces should be recorded as append-only-style events.

PR1 stores traces in memory, but domain methods should treat trace events as historical observations, not mutable current state.

## Consequences

Future persistence should preserve trace event order and identity.
