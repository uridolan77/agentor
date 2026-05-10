# Current PR — harness marker

Completed: Phase 19 PR91-PR95 + PR95.5 — Authorization hardening: alias endpoints now enforce permissions (`GET /runs/{id}/audit-packet`, `POST /reviews/{id}/decisions`), review inbox endpoint guarded with read permission, strict JWT role claim handling (missing/invalid role rejected), docs clarified that Jwt mode consumes an already-authenticated principal and does not configure JwtBearer validation itself, and focused alias/JWT hardening tests added. Full verification: restore/build/test succeeded; verify-harness and verify-repo-clean passed.

Next: Phase 20 or next explicitly scheduled phase.

Do not start the next phase during closeout.
