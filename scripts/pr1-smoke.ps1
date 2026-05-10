$ErrorActionPreference = "Stop"

$baseUrl = "http://localhost:5000"

Write-Host "Checking health..."
Invoke-RestMethod "$baseUrl/health"

Write-Host "Creating agent run..."
$body = @{
  agentName = "PR1 Smoke Agent"
  objective = "Prove the deterministic Agentor PR1 runtime."
  traceId = "smoke-" + [guid]::NewGuid().ToString("N")
} | ConvertTo-Json

$run = Invoke-RestMethod "$baseUrl/agent-runs" -Method Post -ContentType "application/json" -Body $body
$run | ConvertTo-Json -Depth 20

Write-Host "Fetching manifest..."
$manifest = Invoke-RestMethod "$baseUrl/agent-runs/$($run.id)/manifest"
$manifest | ConvertTo-Json -Depth 20
