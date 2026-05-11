#!/usr/bin/env pwsh
# Generates Phase 32 CI evaluation artifacts (evaluation-report.md/json + evaluation-summary.csv)
# under artifacts/evaluation when AGENTOR_EVAL_CI_OUT is set, by running the focused xUnit test.

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path $PSScriptRoot -Parent
$testProj = Join-Path $repoRoot "tests" "Agentor.Application.Tests" "Agentor.Application.Tests.csproj"

$out = $env:AGENTOR_EVAL_CI_OUT
if ([string]::IsNullOrWhiteSpace($out)) {
    $out = Join-Path $repoRoot "artifacts" "evaluation"
}

New-Item -ItemType Directory -Force -Path $out | Out-Null
$env:AGENTOR_EVAL_CI_OUT = $out

dotnet build $testProj --configuration Release
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

dotnet test $testProj --configuration Release --no-build --filter "FullyQualifiedName~EvaluationCiArtifactsTests.Writes_ci_evaluation_artifacts"

if ($LASTEXITCODE -ne 0) {
    throw "Evaluation CI artifact generation failed with exit code $LASTEXITCODE"
}

Write-Host "Wrote evaluation artifacts to $out"
