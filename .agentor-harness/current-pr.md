# Current PR — harness marker

Completed: Phase 30 **PR121.5** — **Phase 28–30 finalization**: **`AgentRun.Complete`** sets **`CompletedAt`** and clears **`TerminalAt`** (fail/reject paths keep **`TerminalAt`**); **`AgentRun.Reconstitute`** + **`RecordMapper.ToSummary`** drop stale **`terminal_at`** on **`Completed`** loads; **`JwtAllowUnvalidatedTokensOutsideDevelopment`** gates **`JwtAcceptUnvalidatedBearerTokens`** outside Development/Test; **`OpenApi`** **`MapOpenApi`** after **`Build`** using merged config — Development/Test always on, Production requires **`Agentor:OpenApi:Enabled`**; **`ToolPayload.FromPersistedJson`** v2/malformed-safe paths + tests (EF structured round-trip, audit summary redaction, scalar **`ToolCallStarted`** trace); **`verify-repo-clean`** mojibake scan; security docs updated. Verification: restore/build/test — **482 passed**; harness scripts **ExpectedPhase 30 / PR121.5**.

Next: Phase 31 (**PR122** human-review handler refactor) when explicitly scheduled — confirm branch state vs **`src/Agentor.Application/HumanReview/`** before advancing harness.

Do not start the next phase during closeout.
