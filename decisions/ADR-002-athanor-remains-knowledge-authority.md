# ADR-002 — Athanor Remains the Knowledge Authority

## Status

Accepted

## Decision

Athanor remains the canonical knowledge-state and provenance service.

Agentor may read from Athanor, submit candidate material to Athanor, and queue review items in Athanor.

Agentor must not directly canonize knowledge, resolve contradictions, create canonical snapshots, or bypass project-local authority.

## Consequences

Agentor integrates with Athanor through client ports only.

No Athanor runtime dependency is included in PR1.
