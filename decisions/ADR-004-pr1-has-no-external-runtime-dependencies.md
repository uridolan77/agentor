# ADR-004 — PR1 Has No External Runtime Dependencies

## Status

Accepted

## Decision

PR1 uses only in-memory storage, a fake tool executor, and an allow-all policy evaluator.

## Consequences

PR1 can be built and tested without PostgreSQL, Athanor, Conexus, MCP, Redis, vector stores, or model credentials.
