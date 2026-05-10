#!/usr/bin/env bash
log="${VERIFY_READ_LOG:-./.claude/.evidence-reads}"
path=$(cat | python3 -c 'import json,sys; print(json.load(sys.stdin).get("tool_input",{}).get("file_path",""))' 2>/dev/null)
case "$path" in
  *artifacts/verification/*|*screenshots/*|*-console.txt|*-result.txt|*.png)
    [ -f "$path" ] && mkdir -p "$(dirname "$log")" && echo "$path" >> "$log"
    ;;
esac
exit 0
