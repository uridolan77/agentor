$log = if ($env:VERIFY_READ_LOG) { $env:VERIFY_READ_LOG } else { "./.claude/.evidence-reads" }

$inputJson = [Console]::In.ReadToEnd()
try {
  $obj = $inputJson | ConvertFrom-Json
  $path = $obj.tool_input.file_path
} catch {
  exit 0
}

if (-not $path) { exit 0 }

$normalized = $path -replace '\\','/'

if (
  $normalized -like "*artifacts/verification/*" -or
  $normalized -like "*screenshots/*" -or
  $normalized -like "*-console.txt" -or
  $normalized -like "*-result.txt" -or
  $normalized -like "*.png"
) {
  if (Test-Path $path) {
    New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
    Add-Content -Path $log -Value $path
  }
}
exit 0
