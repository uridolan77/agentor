# Phase 3 — Tools and runtime policy

## PRs

- PR11 — Tool registry and tool definitions
- PR12 — Runtime policy engine v1
- PR13 — Tool execution pipeline with timeout/retry/cancellation
- PR14 — Tool result envelopes and artifact references
- PR15 — Tool-call audit and policy denial surfaces

## Phase-end audit

After this phase, ask Claude Code:

```text
Perform a phase boundary audit. Check CWC decomposition, Athanor/Conexus/MCP boundaries, external framework adapter strategy, missing tests, and scope creep. Do not edit files until I approve a remediation plan.
```
