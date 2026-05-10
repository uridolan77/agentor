# Agentor PR1–PR40 Claude Code Package v2

Generated: 2026-05-10

This is a recompiled package after reviewing the current Agentor starter repo docs.

## What changed from v1

This version explicitly incorporates:

- Anthropic CWC workshop lessons:
  - decompose agents into tools, skills, memory, evals, policies, traces, and subagents later
  - do not build a giant agent prompt
  - evals must appear earlier, not only late in the roadmap
  - skills are not tools
  - every run/tool/model action must be traceable
- Framework strategy:
  - Microsoft Agent Framework / Semantic Kernel / A2A / MCP / LangGraph / AutoGen are adapters, not Agentor core
  - A2A is post-v0.1 unless a real requirement appears earlier
  - MCP enters only through tool-registry boundaries
- Current Agentor starter docs alignment:
  - `docs/ROADMAP.md` in the starter is too short and should be replaced or superseded
  - `AGENTS.md` says "small and vertical"; this package changes the doctrine to "medium-long, coherent, reviewable passes"
  - service-boundary docs need a framework/adapters section
  - add ADR-006 for external frameworks as adapters

## Package layout

```text
DOCUMENT_CHANGE_REPORT.md
INSTALL_INSTRUCTIONS.md
OVERLAY_FILES/
  AGENTS.md
  PROJECT_CHARTER.md
  docs/
  decisions/
docs/planning/pr1-pr40/
  CLAUDE.md
  00_START_HERE.md
  PR_INDEX.md
  MASTER_ROADMAP.md
  prs/
  phases/
  templates/
  scripts/
```

## Recommended use

Copy `OVERLAY_FILES/*` into the repo root to update the starter docs.

Then copy `docs/planning/pr1-pr40/` into the repo as the long roadmap package.

Do not ask Claude Code to implement all 40 PRs at once.

---

*Archived from the repository root `README.md` (2026-05-11, Phase 23). The root README now describes the Agentor product.*
