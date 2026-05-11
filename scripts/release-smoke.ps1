# Phase 36 / PR146 + PR148.5 — release-oriented HTTP smoke against a running Agentor.Api instance.
# Expects Fake auth (default Development) so authenticated routes succeed without extra headers.
# For Header mode, pass -HeaderActorId (GUID) and ensure appsettings use Agentor:Auth:Mode=Header.
param(
    [string] $BaseUrl = "http://localhost:8080",
    [string] $HeaderActorId = "",
    [string] $HeaderName = "X-Agentor-Actor-Id",
    [string] $OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$root = $BaseUrl.TrimEnd("/")
$results = New-Object System.Collections.Generic.List[object]
$overallOk = $true

function Add-StepResult([string] $name, [string] $method, [string] $uri, [int] $expected, [int] $actual, [bool] $ok, [string] $detail) {
    $results.Add([pscustomobject]@{
        name = $name
        method = $method
        uri = $uri
        expectedStatus = $expected
        actualStatus = $actual
        ok = $ok
        detail = $detail
    }) | Out-Null
}

function Invoke-SmokeGet([string] $name, [string] $relativePath, [int] $expected = 200) {
    $uri = $root + $relativePath
    $headers = @{}
    if (-not [string]::IsNullOrWhiteSpace($HeaderActorId)) {
        $headers[$HeaderName] = $HeaderActorId
    }
    try {
        $response = Invoke-WebRequest -Uri $uri -UseBasicParsing -Headers $headers -TimeoutSec 60
    } catch {
        Add-StepResult $name "GET" $uri $expected 0 $false ("exception: " + $_.Exception.Message)
        $script:overallOk = $false
        throw
    }
    $ok = ($response.StatusCode -eq $expected)
    Add-StepResult $name "GET" $uri $expected $response.StatusCode $ok ""
    if (-not $ok) {
        $script:overallOk = $false
        throw "Expected HTTP $expected from $uri, got $($response.StatusCode)"
    }
    return $response
}

Write-Host "Release smoke: GET $root/health"
Invoke-SmokeGet "health" "/health" | Out-Null

Write-Host "Release smoke: GET $root/ready"
Invoke-SmokeGet "ready" "/ready" | Out-Null

Write-Host "Release smoke: GET $root/api/v1/integrations/status"
Invoke-SmokeGet "integrations-status" "/api/v1/integrations/status" | Out-Null

$body = @{
    agentName = "release-smoke"
    objective = "Phase 36 PR146 scripted smoke"
} | ConvertTo-Json

Write-Host "Release smoke: POST $root/api/v1/agent-runs"
$postHeaders = @{ "Content-Type" = "application/json" }
if (-not [string]::IsNullOrWhiteSpace($HeaderActorId)) {
    $postHeaders[$HeaderName] = $HeaderActorId
}
$postUri = $root + "/api/v1/agent-runs"
try {
    $postResponse = Invoke-WebRequest -Uri $postUri -Method Post -Body $body -Headers $postHeaders -UseBasicParsing -TimeoutSec 120
} catch {
    Add-StepResult "agent-run-start" "POST" $postUri 202 0 $false ("exception: " + $_.Exception.Message)
    $overallOk = $false
    throw
}
$postOk = ($postResponse.StatusCode -eq 202)
Add-StepResult "agent-run-start" "POST" $postUri 202 $postResponse.StatusCode $postOk ""
if (-not $postOk) {
    $overallOk = $false
    throw "Expected HTTP 202 from POST agent-runs, got $($postResponse.StatusCode)"
}
$run = $postResponse.Content | ConvertFrom-Json
$runId = [string]$run.id
if ([string]::IsNullOrWhiteSpace($runId)) {
    $overallOk = $false
    throw "POST agent-runs did not return a run id in JSON body."
}

Write-Host "Release smoke: GET $root/api/v1/agent-runs/$runId"
Invoke-SmokeGet "agent-run-get" "/api/v1/agent-runs/$runId" | Out-Null

Write-Host "Release smoke: GET $root/api/v1/agent-runs/$runId/trace"
Invoke-SmokeGet "agent-run-trace" "/api/v1/agent-runs/$runId/trace" | Out-Null

Write-Host "Release smoke: GET $root/api/v1/agent-runs/$runId/audit-export"
Invoke-SmokeGet "agent-run-audit-export" "/api/v1/agent-runs/$runId/audit-export" | Out-Null

Write-Host "Release smoke: GET $root/api/v1/operator/dashboard"
Invoke-SmokeGet "operator-dashboard" "/api/v1/operator/dashboard" | Out-Null

Write-Host "Release smoke OK (run id: $runId)"

if (-not [string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $dir = $OutputDirectory.TrimEnd("/", "\")
    if (-not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    $report = [pscustomobject]@{
        generatedAt = (Get-Date).ToUniversalTime().ToString("o")
        baseUrl = $root
        runId = $runId
        overallOk = $overallOk
        steps = $results
    }

    $jsonPath = Join-Path $dir "release-smoke-report.json"
    $mdPath = Join-Path $dir "release-smoke-report.md"
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $jsonPath -Encoding UTF8

    $lines = @("# Release smoke report", "", "Generated: $($report.generatedAt)", "Base URL: $root", "Run id: $runId", "Overall: $(if ($overallOk) { "OK" } else { "FAILED" })", "", "| Step | Method | URI | Expected | Actual | OK |", "|------|--------|-----|----------|--------|-----|")
    foreach ($s in $results) {
        $lines += "| $($s.name) | $($s.method) | $($s.uri) | $($s.expectedStatus) | $($s.actualStatus) | $($s.ok) |"
    }
    $lines -join "`n" | Set-Content -LiteralPath $mdPath -Encoding UTF8
    Write-Host "Release smoke report: $jsonPath"
}
