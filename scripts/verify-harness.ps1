param(
    [string]$ExpectedPhase = "",
    [string]$ExpectedHarnessPass = ""
)

$ErrorActionPreference = "Stop"

$files = @(
    ".agentor-harness/current-pr.md",
    ".agentor-harness/feature-list.json",
    ".agentor-harness/progress.md",
    ".agentor-harness/verification-log.md",
    ".agentor-harness/session-handoff.md"
)

foreach ($file in $files) {
    if (!(Test-Path $file)) {
        throw "Missing harness file: $file"
    }

    $bytes = [System.IO.File]::ReadAllBytes($file)

    if ($bytes.Length -ge 2) {
        for ($i = 1; $i -lt $bytes.Length; $i += 2) {
            if ($bytes[$i] -eq 0) {
                throw "Possible UTF-16/null-byte encoding detected: $file"
            }
        }
    }

    $utf8Strict = [System.Text.UTF8Encoding]::new($false, $true)
    try {
        $text = $utf8Strict.GetString($bytes)
    } catch {
        throw "File is not valid strict UTF-8: $file"
    }

    if ([string]::IsNullOrWhiteSpace($text)) {
        throw "Empty harness file: $file"
    }
}

$current = Get-Content ".agentor-harness/current-pr.md" -Raw -Encoding UTF8
if ($current -notmatch "Completed:") {
    throw "current-pr.md missing Completed line"
}
if ($current -notmatch "Next:") {
    throw "current-pr.md missing Next line"
}

$featureRaw = Get-Content ".agentor-harness/feature-list.json" -Raw -Encoding UTF8
try {
    $feature = $featureRaw | ConvertFrom-Json
} catch {
    throw "feature-list.json is not valid JSON"
}

if ($ExpectedPhase -ne "" -and "$($feature.phase)" -ne $ExpectedPhase) {
    throw "feature-list.json phase '$($feature.phase)' does not match expected '$ExpectedPhase'"
}

if ($ExpectedHarnessPass -ne "" -and "$($feature.harnessPass)" -ne $ExpectedHarnessPass) {
    throw "feature-list.json harnessPass '$($feature.harnessPass)' does not match expected '$ExpectedHarnessPass'"
}

if ($null -eq $feature.acceptanceItems -or $feature.acceptanceItems.Count -eq 0) {
    throw "feature-list.json has no acceptanceItems"
}

foreach ($item in $feature.acceptanceItems) {
    if ([string]::IsNullOrWhiteSpace($item.id)) {
        throw "Acceptance item missing id"
    }
    if ([string]::IsNullOrWhiteSpace($item.description)) {
        throw "Acceptance item '$($item.id)' missing description"
    }
    if ($null -eq $item.passes) {
        throw "Acceptance item '$($item.id)' missing passes"
    }
    if ($item.passes -eq $true -and [string]::IsNullOrWhiteSpace($item.evidence)) {
        throw "Acceptance item '$($item.id)' is true but missing evidence"
    }
}

$verification = Get-Content ".agentor-harness/verification-log.md" -Raw -Encoding UTF8
if ($verification -notmatch "dotnet restore Agentor\.sln") {
    throw "verification-log.md missing dotnet restore command"
}
if ($verification -notmatch "dotnet build Agentor\.sln") {
    throw "verification-log.md missing dotnet build command"
}
if ($verification -notmatch "dotnet test Agentor\.sln") {
    throw "verification-log.md missing dotnet test command"
}

$handoff = Get-Content ".agentor-harness/session-handoff.md" -Raw -Encoding UTF8
if ($handoff -notmatch "Not started|not started|No Phase|Phase .* was not started|was not started") {
    throw "session-handoff.md should explicitly say what was not started"
}

Write-Host "Harness verification passed."
