#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Phase 39 PR160 — local HTTP load smoke against a running Agentor.Api (not a production load tool).

.PARAMETER BaseUrl
  Root URL of the API (default http://127.0.0.1:5050/).

.PARAMETER RunCount
  Number of POST /api/v1/agent-runs invocations (default 20).

.PARAMETER QueueCount
  Number of POST /api/v1/agent-runs/queued invocations after the run phase (default 0).

.PARAMETER Concurrency
  Maximum parallel in-flight HTTP calls (PowerShell 7+ parallel; default 4).

.PARAMETER Workload
  fake | review | required | mixed — shapes the POST body (default fake). With -StartHost and review, sets MaxAutoApproveRisk=Low for high-risk tool path.

.PARAMETER OutputDirectory
  When set, writes load-smoke-report.json here.

.PARAMETER StartHost
  Builds and starts Agentor.Api on BaseUrl (Development). Child process is stopped when the script ends.

.PARAMETER ActorHeaderValue
  Optional X-Agentor-Actor-Id for Header auth deployments.
#>
param(
    [Uri] $BaseUrl = [Uri]::new("http://127.0.0.1:5050/"),
    [int] $RunCount = 20,
    [int] $QueueCount = 0,
    [int] $Concurrency = 4,
    [ValidateSet("fake", "review", "required", "mixed")]
    [string] $Workload = "fake",

    [string] $OutputDirectory = "",

    [switch] $StartHost,

    [string] $ActorHeaderValue = "33333333-3333-4333-8333-333333333333",

    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"
if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw "PowerShell 7+ is required (ForEach-Object -Parallel)."
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$apiProj = Join-Path $repoRoot "src/Agentor.Api/Agentor.Api.csproj"

$child = $null
if ($StartHost) {
    dotnet restore $apiProj | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Restore failed" }
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    if ($Workload -eq "review") {
        $env:Agentor__RuntimePolicy__MaxAutoApproveRisk = "Low"
    }
    $urlArg = $BaseUrl.ToString().TrimEnd('/')
    Write-Host "Starting API: $urlArg"
    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = "dotnet"
    $psi.Arguments = "run --project `"$apiProj`" -c $Configuration --no-restore --urls `"$urlArg/`""
    $psi.WorkingDirectory = $repoRoot
    $psi.UseShellExecute = $false
    $child = [System.Diagnostics.Process]::Start($psi)
    $health = [Uri]::new($BaseUrl, "health")
    $ok = $false
    foreach ($i in 0..80) {
        try {
            $r = Invoke-WebRequest -Uri $health -UseBasicParsing -TimeoutSec 2
            if ($r.StatusCode -eq 200) { $ok = $true; break }
        } catch { Start-Sleep -Milliseconds 400 }
    }
    if (-not $ok) {
        if ($null -ne $child -and -not $child.HasExited) { $child.Kill($true) }
        throw "API did not become healthy at $health"
    }
}

$root = $BaseUrl.ToString().TrimEnd('/') + "/"
$runsUri = [Uri]::new($root + "api/v1/agent-runs").ToString()
$queuedUri = [Uri]::new($root + "api/v1/agent-runs/queued").ToString()

$swTotal = [System.Diagnostics.Stopwatch]::StartNew()
$results = [System.Collections.Concurrent.ConcurrentBag[object]]::new()

0..([Math]::Max(0, $RunCount - 1)) | ForEach-Object -ThrottleLimit $Concurrency -Parallel {
    $idx = $_
    $uri = $using:runsUri
    $wl = $using:Workload
    $actor = $using:ActorHeaderValue
    $bodyHash = @{
        fake     = @{ agentName = "LoadSmoke"; objective = "fake $idx"; traceId = "ls-$idx-" + [Guid]::NewGuid().ToString("N") }
        review   = @{ agentName = "LoadSmoke"; objective = "rev $idx"; traceId = "ls-$idx-" + [Guid]::NewGuid().ToString("N"); toolKey = "pr1.high-risk-fake-tool"; mode = "SingleTool" }
        required = @{ agentName = "LoadSmoke"; objective = "req $idx"; traceId = "ls-$idx-" + [Guid]::NewGuid().ToString("N"); toolKey = "conexus.model-complete"; mode = "SingleTool" }
    }
    $body = $null
    if ($wl -eq "mixed") {
        $m = $idx % 3
        if ($m -eq 0) { $body = @{ agentName = "LoadSmoke"; objective = "mix-fake $idx"; traceId = "mix-$idx-" + [Guid]::NewGuid().ToString("N") } }
        elseif ($m -eq 1) { $body = @{ agentName = "LoadSmoke"; objective = "mix-rev $idx"; traceId = "mix-$idx-" + [Guid]::NewGuid().ToString("N"); toolKey = "pr1.high-risk-fake-tool"; mode = "SingleTool" } }
        else { $body = @{ agentName = "LoadSmoke"; objective = "mix-req $idx"; traceId = "mix-$idx-" + [Guid]::NewGuid().ToString("N"); toolKey = "conexus.model-complete"; mode = "SingleTool" } }
    }
    else {
        $body = $bodyHash[$wl]
    }
    $headers = @{ }
    if (-not [string]::IsNullOrWhiteSpace($actor)) {
        $headers["X-Agentor-Actor-Id"] = $actor
    }
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $null = Invoke-RestMethod -Method Post -Uri $uri -Body ($body | ConvertTo-Json) -ContentType "application/json" -Headers $headers -TimeoutSec 120
        $sw.Stop()
        ($using:results).Add([pscustomobject]@{ ok = $true; ms = $sw.Elapsed.TotalMilliseconds; error = $null })
    }
    catch {
        $sw.Stop()
        ($using:results).Add([pscustomobject]@{ ok = $false; ms = $sw.Elapsed.TotalMilliseconds; error = $_.Exception.Message })
    }
}

foreach ($q in 0..([Math]::Max(0, $QueueCount - 1))) {
    $qb = @{ agentName = "LoadQ"; objective = "queued $q"; traceId = "q-$q-" + [Guid]::NewGuid().ToString("N") }
    $headers = @{ }
    if (-not [string]::IsNullOrWhiteSpace($ActorHeaderValue)) {
        $headers["X-Agentor-Actor-Id"] = $ActorHeaderValue
    }
    try {
        $null = Invoke-RestMethod -Method Post -Uri $queuedUri -Body ($qb | ConvertTo-Json) -ContentType "application/json" -Headers $headers -TimeoutSec 120
    }
    catch {
        $results.Add([pscustomobject]@{ ok = $false; ms = 0; error = "queued: $($_.Exception.Message)" })
    }
}

$swTotal.Stop()
$okCount = ($results | Where-Object { $_.ok }).Count
$failCount = ($results | Where-Object { -not $_.ok }).Count
$msList = @($results | Where-Object { $_.ok } | ForEach-Object { $_.ms })
$meanMs = if ($msList.Count -gt 0) { ($msList | Measure-Object -Average).Average } else { 0 }

$report = [ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    baseUrl        = $root
    workload       = $Workload
    runCount       = $RunCount
    queueCount     = $QueueCount
    concurrency    = $Concurrency
    succeeded      = $okCount
    failed         = $failCount
    elapsedSeconds = [Math]::Round($swTotal.Elapsed.TotalSeconds, 3)
    meanLatencyMs  = [Math]::Round($meanMs, 3)
    errors         = @($results | Where-Object { -not $_.ok } | ForEach-Object { $_.error } | Select-Object -First 20)
}

Write-Host ($report | ConvertTo-Json -Depth 5)

if (-not [string]::IsNullOrWhiteSpace($OutputDirectory)) {
    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
    $outJson = Join-Path $OutputDirectory "load-smoke-report.json"
    $report | ConvertTo-Json -Depth 6 | Set-Content -Path $outJson -Encoding utf8
    Write-Host "Wrote $outJson"
}

if ($null -ne $child -and -not $child.HasExited) {
    try { $child.Kill($true) } catch { }
}

if ($failCount -gt 0) {
    exit 2
}
