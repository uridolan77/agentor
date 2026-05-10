$log = if ($env:VERIFY_READ_LOG) { $env:VERIFY_READ_LOG } else { "./.claude/.evidence-reads" }
$results = if ($env:RESULTS_FILE) { $env:RESULTS_FILE } else { "test-results.agentor.json" }

$inputJson = [Console]::In.ReadToEnd()
try {
  $obj = $inputJson | ConvertFrom-Json
  $target = $obj.tool_input.file_path
} catch {
  exit 0
}

if (-not $target) { exit 0 }

$targetName = Split-Path $target -Leaf
$resultsName = Split-Path $results -Leaf

if ($targetName -ne $resultsName) { exit 0 }

if (!(Test-Path $log) -or ((Get-Item $log).Length -eq 0)) {
  '{"decision":"block","reason":"Cannot modify test-results.agentor.json: no verification evidence file has been Read this session. Open artifacts/verification/*.txt with the Read tool first."}'
  exit 0
}

Clear-Content $log
exit 0
