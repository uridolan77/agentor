# Repository cleanliness checks (PR75.7). Prints every offending path; exits non-zero on failure.
# Run from repository root. Does not modify files.
# UTF-8/BOM/null-byte checks cover harness, automation, docs, source, tests, benchmarks, optional fixtures,
# root policy files, and explicit root globs (UTF-8 no BOM).

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$failures = New-Object System.Collections.Generic.List[string]
$blockedHarnessSnapshotFiles = @(
    "feature-list.json.head.txt"
)

function Add-Failure([string]$Message) {
    [void]$failures.Add($Message)
}

function Test-TextFileEncoding([string]$fullPath) {
    $bytes = [System.IO.File]::ReadAllBytes($fullPath)
    if ($bytes.Length -eq 0) { return }

    # UTF-16 BOM
    if (($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) -or
        ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF)) {
        Add-Failure "UTF-16 BOM detected (rewrite as UTF-8): $fullPath"
        return
    }

    # UTF-8 BOM (repo standard: no BOM)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        Add-Failure "UTF-8 BOM detected (use UTF-8 without BOM): $fullPath"
        return
    }

    for ($i = 0; $i -lt $bytes.Length; $i++) {
        if ($bytes[$i] -eq 0) {
            Add-Failure "Null byte detected in text file: $fullPath"
            return
        }
    }

    $utf8Strict = [System.Text.UTF8Encoding]::new($false, $true)
    try {
        [void]$utf8Strict.GetString($bytes)
    } catch {
        Add-Failure "File is not valid strict UTF-8: $fullPath ($($_.Exception.Message))"
    }
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

# --- Harness / policy checks ---
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

$harnessRoot = Join-Path $repoRoot ".agentor-harness"
if (Test-Path -LiteralPath $harnessRoot) {
    foreach ($name in $blockedHarnessSnapshotFiles) {
        $blockedPath = Join-Path $harnessRoot $name
        if (Test-Path -LiteralPath $blockedPath) {
            Add-Failure "Stale harness snapshot file detected (delete it): $blockedPath"
        }
    }
}

# --- Broad UTF-8 / BOM / encoding scan (PR75.7) ---
$textExtensions = @(
    ".cs", ".md", ".json", ".yml", ".yaml", ".ps1", ".sln", ".csproj",
    ".props", ".targets", ".editorconfig", ".gitignore"
)

$scanRoots = @(
    (Join-Path $repoRoot ".agentor-harness"),
    (Join-Path $repoRoot ".cursor"),
    (Join-Path $repoRoot "scripts"),
    (Join-Path (Join-Path $repoRoot ".github") "workflows"),
    (Join-Path $repoRoot "docs"),
    (Join-Path $repoRoot "src"),
    (Join-Path $repoRoot "tests"),
    (Join-Path $repoRoot "benchmarks")
)

$fixturesRoot = Join-Path $repoRoot "fixtures"
if (Test-Path -LiteralPath $fixturesRoot) {
    $scanRoots += $fixturesRoot
}

foreach ($root in $scanRoots) {
    if (-not (Test-Path -LiteralPath $root)) { continue }
    Get-ChildItem -LiteralPath $root -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
        $full = $_.FullName
        if ($full -match "\\(bin|obj|\.git|node_modules)\\") { return }
        $ext = $_.Extension.ToLowerInvariant()
        $isHarnessTextFile = $full.StartsWith($harnessRoot, [System.StringComparison]::OrdinalIgnoreCase) -and $ext -eq ".txt"
        $allowed = ($textExtensions -contains $ext) -or ($_.Name -eq ".gitignore") -or $isHarnessTextFile
        if (-not $allowed) { return }
        Test-TextFileEncoding $full
    }
}

# Root-level globs and named files (repo policy / solution entrypoints)
foreach ($pat in @("*.sln", "*.md", "*.json", "*.yml", "*.yaml", "*.props", "*.targets", "*.csproj")) {
    Get-ChildItem -LiteralPath $repoRoot -File -Filter $pat -ErrorAction SilentlyContinue | ForEach-Object {
        Test-TextFileEncoding $_.FullName
    }
}

foreach ($name in @("Dockerfile", "docker-compose.yml", ".editorconfig", ".gitignore")) {
    $rp = Join-Path $repoRoot $name
    if (Test-Path -LiteralPath $rp) {
        Test-TextFileEncoding $rp
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
