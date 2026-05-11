# Durable run queue tool payloads

Queued runs persist **`StartAgentRunCommand`** so a worker can replay **`POST /api/v1/agent-runs`** semantics after dequeue.

## Columns (`run_queue_items`)

| Column | Meaning |
| --- | --- |
| **`tool_input_json`** | Legacy JSON object of **string key → string value** pairs (pre–structured payload). Still written as a **summary projection** when a structured payload is enqueued. |
| **`tool_payload_json`** | **ToolPayload v2** JSON (`body`, `schemaId`, `contentType`, `summary`) matching persisted tool I/O elsewhere. |

When **`tool_payload_json` is non-empty**, replay uses **`ToolInputPayload`** and ignores the legacy dictionary column for orchestration (the legacy column may still contain summary scalars for operators).

## HTTP request shape

**`StartAgentRunRequestDto`** accepts optional **`toolInputPayload`**: a JSON object in the same shape as persisted **`ToolPayload`** (see domain **`ToolPayload.ToPersistedJson`**). When **`toolInputPayload`** is present, it **overrides** flattened **`input`**.

## Idempotency / fingerprint

**`StartAgentRunFingerprint`** includes both flattened **`input`** and raw **`toolInputPayload`** JSON so distinct structured payloads do not collide with legacy-only fingerprints.

## References

- **`RunQueuePayloadSerialization`** — serialize/deserialize queue **`tool_payload_json`**.
- **`RunQueuePayloadVersion`** — enum marker (**legacy string-keyed** vs **structured ToolPayload**).
- Fixture example: **`tests/Agentor.Application.Tests/fixtures/eval/queued-structured-toolpayload.json`**.
