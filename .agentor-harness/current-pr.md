# Current PR — harness marker

Completed: Phase 21 PR101–PR105 — Integration contract conformance: expanded stub-handler contract tests for Athanor, Conexus, MCP, and external-agent HTTP adapters (404/null, non-2xx errors, telemetry and declared-budget JSON, timeout/cancel); `ModelCallRequestDto` + `ModelGatewayToolExecutor` pass-through for declared cost/latency; `HttpModelGatewayClient` and `HttpExternalAgentProtocolClient` use structured `HttpRequestException` bodies; `ExternalAgentInvokeToolExecutor` surfaces `isNonCanonEvidence`; plan-level tests prove policy deny/review blocks external protocol invocation; `docs/integrations/compatibility-matrix.md`; `McpDescriptorDomainBoundaryTests`. Full verification: restore/build/test (394 tests) and harness scripts passed with ExpectedPhase 21 / PR105.

Next: Phase 22 or next explicitly scheduled phase.

Do not start the next phase during closeout.
