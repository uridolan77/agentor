#!/usr/bin/env bash
if [ -e "${AGENT_STOP_FILE:-./AGENT_STOP}" ]; then
  cat <<'JSON'
{"decision":"block","reason":"Kill switch engaged: AGENT_STOP file exists. Remove the file to resume."}
JSON
fi
exit 0
