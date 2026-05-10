# Session memory boundary (Agentor)

Agentor `AgentRun.SessionMemory` is **run-scoped operational scratch**, not canonical knowledge.

- **Run-scoped:** Values are keyed strings attached to a single `AgentRun` for the lifetime of that run (and optional persistence as operational state, for example via `session_memory_json` on the run row). They exist to pass forward cheap facts (tool outputs, planner scratch, user-supplied run parameters) inside coordination.
- **Non-canon:** Nothing written here is treated as durable truth, policy-exempt evidence, or merged into a knowledge graph. Tool output remains evidence or candidate material per project doctrine; session memory is an additional scratch channel, not a promotion path to canon.
- **Not Athanor:** Athanor remains the canonical knowledge-state and provenance service. Session memory must never be described or implemented as a substitute for Athanor reads, writes, canonization, or review queues.
- **Budgeted:** `SessionMemoryBudget` caps keys, key length, value length, and total stored characters. Rejections emit `SessionMemoryWriteRejected` traces; accepts emit `SessionMemoryWriteAccepted`.

Plans consume session values by prefixing keys in tool input dictionaries as `session:{key}` (see `PlanInputBuilder` / `SequentialAgentPlanExecutor`).