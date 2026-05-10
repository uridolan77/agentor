# Install Instructions

## Option A — Copy everything manually

Copy the contents of:

```text
OVERLAY_FILES/
```

into the Agentor repo root.

Then copy:

```text
docs/planning/pr1-pr40/
```

into:

```text
<agentor>/docs/planning/pr1-pr40/
```

## Option B — PowerShell

From the extracted package root:

```powershell
$repo = "C:\dev\agentor"

Copy-Item ".\OVERLAY_FILES\*" $repo -Recurse -Force
New-Item -ItemType Directory -Force "$repo\docs\planning\pr1-pr40"
Copy-Item ".\docs\planning\pr1-pr40\*" "$repo\docs\planning\pr1-pr40" -Recurse -Force
```

## After install

Ask Claude Code:

```text
Review the updated Agentor docs:
- AGENTS.md
- PROJECT_CHARTER.md
- docs/ARCHITECTURE.md
- docs/SERVICE_BOUNDARIES.md
- docs/FRAMEWORK_STRATEGY.md
- docs/CWC_WORKSHOP_LESSONS_APPLIED.md
- docs/planning/pr1-pr40/PR_INDEX.md

Do not edit files yet.

Report any contradictions, especially around PR sizing, CWC lessons, MCP, A2A, Microsoft Agent Framework, Semantic Kernel, Athanor, and Conexus.
```
