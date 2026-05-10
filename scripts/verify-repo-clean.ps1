# Repository cleanliness checks (PR75.6). Conservative: prints every offending path.
# Run from repository root. Does not modify files.
# UTF-8/BOM/null-byte checks are intentionally limited to `.agentor-harness/`, `scripts/`, and
# `.github/workflows/` so the script does not fail on legacy encoding elsewhere in `src/` or `docs/`.

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$failures = New-Object System.Collections.Generic.List[string]

function Add-Failure([string]$Message) {
    [void]$failures.Add($Message)
}

# --- 1) Root-level Python (no allowlist today) ---
$allowlistedRootPy = @()
Get-ChildItem -LiteralPath $repoRoot -File -Filter "*.py" -ErrorAction SilentlyContinue | ForEach-Object {
    if ($allowlistedRootPy -contains $_.Name) { return }
    Add-Failure "Root-level Python file (remove or move under scripts/): $($_.FullName)"
}

# --- 9) Root scratch filename patterns ---
$rootFiles = Get-ChildItem -LiteralPath $repoRoot -File -ErrorAction SilentlyContinue
foreach ($f in $rootFiles) {
    $n = $f.Name
    if ($n -like "*.scratch.*") { Add-Failure "Root scratch pattern (*.scratch.*): $($f.FullName)" }
    if ($n -like "*.tmp") { Add-Failure "Root scratch pattern (*.tmp): $($f.FullName)" }
    if ($n -like "*.bak") { Add-Failure "Root scratch pattern (*.bak): $($f.FullName)" }
    if ($n -like "*.orig") { Add-Failure "Root scratch pattern (*.orig): $($f.FullName)" }
    if ($n -like "write_*_payload.py") { Add-Failure "Root scratch pattern (write_*_payload.py): $($f.FullName)" }
    if ($n -like "_*.py") { Add-Failure "Root scratch pattern (_*.py): $($f.FullName)" }
}

$rootDirs = @("scratch", "tmp", "artifacts/local", "agent-output", "cursor-output")
foreach ($rel in $rootDirs) {
    $p = Join-Path $repoRoot $rel
    if (Test-Path -LiteralPath $p) {
        Add-Failure "Unexpected root directory (remove or add to allowlist if intentional): $p"
    }
}

# --- Harness / policy checks (overlap with verify-harness.ps1 is intentional) ---
$featurePath = Join-Path $repoRoot ".agentor-harness/feature-list.json"
if (-not (Test-Path -LiteralPath $featurePath)) {
    Add-Failure "Missing $featurePath"
} else {
    $featureRaw = Get-Content -LiteralPath $featurePath -Raw -Encoding UTF8
    try {
        $feature = $featureRaw | ConvertFrom-Json
    } catch {
        Add-Failure "feature-list.json is not valid JSON: $($_.Exception.Message)"
        $feature = $null
    }
    if ($null -ne $feature) {
        foreach ($item in $feature.acceptanceItems) {
            if ($item.passes -eq $true -and [string]::IsNullOrWhiteSpace($item.evidence)) {
                Add-Failure "Acceptance item '$($item.id)' has passes=true but empty evidence."
            }
        }
    }
}

$currentPath = Join-Path $repoRoot ".agentor-harness/current-pr.md"
if (-not (Test-Path -LiteralPath $currentPath)) {
    Add-Failure "Missing current-pr.md"
} else {
    $current = Get-Content -LiteralPath $currentPath -Raw -Encoding UTF8
    if ($current -notmatch "(?m)^Completed:\s*.+") { Add-Failure "current-pr.md missing Completed: line" }
    if ($current -notmatch "(?m)^Next:\s*.+") { Add-Failure "current-pr.md missing Next: line" }
}

$verificationPath = Join-Path $repoRoot ".agentor-harness/verification-log.md"
if (-not (Test-Path -LiteralPath $verificationPath)) {
    Add-Failure "Missing verification-log.md"
} else {
    $verification = Get-Content -LiteralPath $verificationPath -Raw -Encoding UTF8
    if ($verification -notmatch "dotnet restore Agentor\.sln") {
        Add-Failure "verification-log.md missing 'dotnet restore Agentor.sln'"
    }
    if ($verification -notmatch "dotnet build Agentor\.sln") {
        Add-Failure "verification-log.md missing 'dotnet build Agentor.sln'"
    }
    if ($verification -notmatch "dotnet test Agentor\.sln") {
        Add-Failure "verification-log.md missing 'dotnet test Agentor.sln'"
    }
}

$handoffPath = Join-Path $repoRoot ".agentor-harness/session-handoff.md"
if (-not (Test-Path -LiteralPath $handoffPath)) {
    Add-Failure "Missing session-handoff.md"
} else {
    $handoff = Get-Content -LiteralPath $handoffPath -Raw -Encoding UTF8
    if ($handoff -notmatch "Not started|not started|No Phase|Phase .* was not started|was not started") {
        Add-Failure "session-handoff.md should explicitly state what was not started"
    }
}

# --- 2–3) UTF-8 / BOM / UTF-16 / null bytes (PR75.6 scope: harness + automation only) ---
# Broader repo files may contain legacy BOM/null issues; do not fail this script on unrelated paths.
$textExtensions = @(".cs", ".md", ".json", ".yml", ".yaml", ".ps1", ".sln", ".csproj")
$scanRoots = @(
    (Join-Path $repoRoot ".agentor-harness"),
    (Join-Path $repoRoot "scripts"),
    (Join-Path (Join-Path $repoRoot ".github") "workflows")
)

foreach ($root in $scanRoots) {
    if (-not (Test-Path -LiteralPath $root)) { continue }
    Get-ChildItem -LiteralPath $root -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
        $full = $_.FullName
        if ($full -match "\\(bin|obj|\.git)\\") { return }
        if ($textExtensions -notcontains $_.Extension.ToLowerInvariant()) { return }

        $bytes = [System.IO.File]::ReadAllBytes($full)
        if ($bytes.Length -eq 0) { return }

        # UTF-16 BOM
        if (($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) -or
            ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF)) {
            Add-Failure "UTF-16 BOM detected (rewrite as UTF-8): $full"
            return
        }

        # UTF-8 BOM (repo standard: no BOM for these paths)
        if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
            Add-Failure "UTF-8 BOM detected (use UTF-8 without BOM): $full"
            return
        }

        # Null byte in text file
        for ($i = 0; $i -lt $bytes.Length; $i++) {
            if ($bytes[$i] -eq 0) {
                Add-Failure "Null byte detected in text file: $full"
                return
            }
        }

        # Strict UTF-8 decode
        $utf8Strict = [System.Text.UTF8Encoding]::new($false, $true)
        try {
            [void]$utf8Strict.GetString($bytes)
        } catch {
            Add-Failure "File is not valid strict UTF-8: $full ($($_.Exception.Message))"
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host "verify-repo-clean FAILED ($($failures.Count) issue(s)):" -ForegroundColor Red
    foreach ($f in $failures) {
        Write-Host " - $f"
    }
    exit 1
}

Write-Host "Repository cleanliness verification passed."
exit 0
