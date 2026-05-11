# Current PR — harness marker

Completed: Phase 30 **PR121** — **Structured tool I/O v2**: **`ToolPayload`** (**`JsonObject` body**, **`schemaId`/`contentType`**, flat **`summary`**) + **`ToolPayloadJsonConverter`**; **`ToolCall`** **`InputPayload`/`OutputPayload`** with persisted v2 JSON (**`ToPersistedJson`/`FromPersistedJson`**) and legacy flat-object load path; **`ToolExecutionRequest`/`ToolExecutionResult`**, **`McpToolInvocationResult`**, **`IMcpRegistryClient.InvokeToolAsync`** use **`ToolPayload`**; **`ModelCallRequestDto`/`ModelCallResultDto`** and **`ExternalAgentInvocationRequestDto`** wrap **`ToolPayload`** (**`FromLegacy`** factories); HTTP MCP invoke posts structured JSON; **`GetRunAuditExportQueryHandler`** emits steps.**`toolCalls`**.**`input`/`output`** as **`body` + `summary` + metadata** (nested redaction); **`docs/REPO_TRUTH.md`** bullet; tests **`ToolPayloadTests`**, **`GetRunAuditExportQueryHandlerTests`** nested secret redaction, updated Conexus/MCP/external-agent tests. Verification: restore/build/test — **461 passed**; harness scripts **ExpectedPhase 30 / PR121**.

Next: Phase 31 (refactor large handlers/orchestrators) or next explicitly scheduled phase.

Do not start the next phase during closeout.
