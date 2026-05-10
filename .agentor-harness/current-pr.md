# Current PR — harness marker

Completed: Phase 21 PR101–PR105 + **PR105.5** — Integration contract conformance plus integration HTTP error-shape hardening: shared `IntegrationHttpError` (`ThrowIfUnsuccessfulAsync`, `RedactAndTruncate`) used by Athanor, Conexus, MCP, and external-agent HTTP clients; non-2xx failures throw `HttpRequestException` with **`StatusCode` populated**; upstream error bodies are **truncated** and **best-effort redacted** (Bearer tokens, JSON `apiKey`/`token`/`password`/`secret`/`authorization`, and `key=value`-style pairs). Tests: `IntegrationHttpErrorTests`; adapter tests assert `StatusCode` and Bearer redaction; `docs/integrations/compatibility-matrix.md` updated. Full verification: restore/build/test and harness scripts with **ExpectedPhase 21 / PR105.5**.

Next: Phase 22 or next explicitly scheduled phase.

Do not start the next phase during closeout.
