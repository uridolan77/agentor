$steerFile = if ($env:AGENT_STEER_FILE) { $env:AGENT_STEER_FILE } else { "./STEER.md" }

if ((Test-Path $steerFile) -and ((Get-Item $steerFile).Length -gt 0)) {
  $note = Get-Content $steerFile -Raw
  Clear-Content $steerFile
  $reason = "OPERATOR STEERING: $note`n`nPause what you were about to do, incorporate this guidance, then continue toward the approved PR goal."
  $payload = @{ decision = "block"; reason = $reason } | ConvertTo-Json -Compress
  Write-Output $payload
}
exit 0
