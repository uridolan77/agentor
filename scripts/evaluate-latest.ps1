param(
  [string]$PrSpec = "docs/planning/pr1-pr40/prs/PR08-run-read-model-query-endpoints.md"
)

$prompt = @"
Review the latest Agentor changes.

Read:
- $PrSpec
- AGENTS.md
- docs/SERVICE_BOUNDARIES.md
- docs/FRAMEWORK_STRATEGY.md
- docs/CWC_WORKSHOP_LESSONS_APPLIED.md
- artifacts/verification/dotnet-build.txt
- artifacts/verification/dotnet-test.txt

Run git diff and git status.

Return PASS or NEEDS_WORK as required by the evaluator protocol.
"@

claude --agent evaluator -p $prompt
