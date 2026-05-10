#!/usr/bin/env bash
log="${VERIFY_READ_LOG:-./.claude/.evidence-reads}"
results="${RESULTS_FILE:-test-results.agentor.json}"

input=$(cat)
target=$(printf '%s' "$input" | python3 -c 'import json,sys; print(json.load(sys.stdin).get("tool_input",{}).get("file_path",""))' 2>/dev/null)

case "$target" in "$results"|*/"$results") ;; *) exit 0 ;; esac

if [ ! -s "$log" ]; then
  cat <<'JSON'
{"decision":"block","reason":"Cannot modify test-results.agentor.json: no verification evidence file has been Read this session. Open artifacts/verification/*.txt with the Read tool first."}
JSON
  exit 0
fi

: > "$log"
exit 0
