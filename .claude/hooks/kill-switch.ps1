$stopFile = if ($env:AGENT_STOP_FILE) { $env:AGENT_STOP_FILE } else { "./AGENT_STOP" }

if (Test-Path $stopFile) {
  '{"decision":"block","reason":"Kill switch engaged: AGENT_STOP file exists. Remove the file to resume."}'
}
exit 0
