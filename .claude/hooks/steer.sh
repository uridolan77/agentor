#!/usr/bin/env bash
f="${AGENT_STEER_FILE:-./STEER.md}"
if [ -s "$f" ]; then
  note=$(cat "$f")
  : > "$f"
  reason=$(python3 -c 'import json,sys; print(json.dumps("OPERATOR STEERING: " + sys.argv[1] + "\n\nPause what you were about to do, incorporate this guidance, then continue toward the approved PR goal."))' "$note" 2>/dev/null) || exit 0
  printf '{"decision":"block","reason":%s}\n' "$reason"
fi
exit 0
