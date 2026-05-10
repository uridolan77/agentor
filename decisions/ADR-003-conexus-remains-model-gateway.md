# ADR-003 — Conexus Remains the Model Gateway

## Status

Accepted

## Decision

Conexus remains responsible for model routing and LLM provider access.

Agentor will call Conexus through an application port in a later PR.

## Consequences

Agentor Domain and Application must not reference provider SDKs.
