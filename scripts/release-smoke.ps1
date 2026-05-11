# Phase 36 / PR146 — release-oriented HTTP smoke against a running Agentor.Api instance.
# Expects Fake auth (default Development) so authenticated routes succeed without extra headers.
# For Header mode, pass -HeaderActorId (GUID) and ensure appsettings use Agentor:Auth:Mode=Header.
param(
    [string] $BaseUrl = "http://localhost:8080",
    [string] $HeaderActorId = "",
    [string] $HeaderName = "X-Agentor-Actor-Id"
)

$ErrorActionPreference = "Stop"
$root = $BaseUrl.TrimEnd("/")

function Invoke-SmokeGet([string] $relativePath, [int] $expected = 200) {
    $uri = $root + $relativePath
    $headers = @{}
    if (-not [string]::IsNullOrWhiteSpace($HeaderActorId)) {
        $headers[$HeaderName] = $HeaderActorId
    }
    $response = Invoke-WebRequest -Uri $uri -UseBasicParsing -Headers $headers -TimeoutSec 60
    if ($response.StatusCode -ne $expected) {
        throw "Expected HTTP $expected from $uri, got $($response.StatusCode)"
    }
    return $response
}

Write-Host "Release smoke: GET $root/health"
Invoke-SmokeGet "/health" | Out-Null

Write-Host "Release smoke: GET $root/ready"
Invoke-SmokeGet "/ready" | Out-Null

Write-Host "Release smoke: GET $root/api/v1/integrations/status"
Invoke-SmokeGet "/api/v1/integrations/status" | Out-Null

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
$postResponse = Invoke-WebRequest -Uri $postUri -Method Post -Body $body -Headers $postHeaders -UseBasicParsing -TimeoutSec 120
if ($postResponse.StatusCode -ne 202) {
    throw "Expected HTTP 202 from POST agent-runs, got $($postResponse.StatusCode)"
}
$run = $postResponse.Content | ConvertFrom-Json
$runId = [string]$run.id
if ([string]::IsNullOrWhiteSpace($runId)) {
    throw "POST agent-runs did not return a run id in JSON body."
}

Write-Host "Release smoke: GET $root/api/v1/agent-runs/$runId"
Invoke-SmokeGet "/api/v1/agent-runs/$runId" | Out-Null

Write-Host "Release smoke: GET $root/api/v1/agent-runs/$runId/trace"
Invoke-SmokeGet "/api/v1/agent-runs/$runId/trace" | Out-Null

Write-Host "Release smoke: GET $root/api/v1/agent-runs/$runId/audit-export"
Invoke-SmokeGet "/api/v1/agent-runs/$runId/audit-export" | Out-Null

Write-Host "Release smoke: GET $root/api/v1/operator/dashboard"
Invoke-SmokeGet "/api/v1/operator/dashboard" | Out-Null

Write-Host "Release smoke OK (run id: $runId)"
