# Runs Agentor.IntegrationSmoke (Phase 35). Configure integrations via environment variables; see docs/operator/integration-smoke.md.
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [string] $OutputDirectory = "",

    [string[]] $Target = @()
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$proj = Join-Path $repoRoot "tools/Agentor.IntegrationSmoke/Agentor.IntegrationSmoke.csproj"

$runArgs = @("run", "--project", $proj, "-c", $Configuration, "--no-restore", "--")
if (-not [string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $runArgs += @("--output", $OutputDirectory)
}

foreach ($t in $Target) {
    if (-not [string]::IsNullOrWhiteSpace($t)) {
        $runArgs += @("--target", $t.Trim())
    }
}

Push-Location $repoRoot
try {
    dotnet restore $proj | Out-Host
    dotnet build $proj -c $Configuration --no-restore | Out-Host
    dotnet @runArgs | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Integration smoke failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

Write-Host "Integration smoke completed successfully."
