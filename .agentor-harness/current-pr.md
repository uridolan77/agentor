# Current PR — harness marker

Completed: Phase 33 **PR128–PR132** (structured queue payload upgrade): **`RunQueuePayloadVersion`** + **`RunQueuePayloadSerialization`**; **`StartAgentRunCommand`** / **`RunOrchestrationRequest`** optional **`ToolInputPayload`**; **`StartAgentRunRequestDto.toolInputPayload`** + mapping + **`StartAgentRunFingerprint`** structured segment; **`GovernedSingleToolRunDriver`** merges **`ToolPayload`** for policy + **`ToolCall`** + pipeline; EF **`tool_payload_json`** column (**migration `20260512100000_Phase33QueueStructuredToolPayload`**), **`EfRunQueueStore`** v2 persist + legacy **`tool_input_json`** load; **`docs/operator/queue-payloads.md`**; fixture **`tests/Agentor.Application.Tests/fixtures/eval/queued-structured-toolpayload.json`**; **`docs/REPO_TRUTH.md`** queue columns. Harness: **`phase` 33**, **`harnessPass` PR132**. Verification: restore/build/test — **504 passed** on **`Agentor.sln`**; **`verify-harness`** ExpectedPhase **33** / **PR132**; **`verify-repo-clean`**.

Next: Phase 34 (skill resume support) only when explicitly scheduled.

Do not start the next phase during closeout.
