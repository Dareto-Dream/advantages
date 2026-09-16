#!/usr/bin/env bash
LOG="/c/Users/steak/AppData/Local/Unity/Editor/Editor.log"

result=$(grep -a "\*\*\* Tundra build" "$LOG" | tail -1)

if [ -z "$result" ]; then
  echo "no build recorded in the editor log yet"
  exit 2
fi

if echo "$result" | grep -q "success"; then
  echo "COMPILE OK  -- $result"
  exit 0
fi

echo "COMPILE FAILED -- $result"
echo
start=$(grep -an "\*\*\* Tundra build" "$LOG" | tail -2 | head -1 | cut -d: -f1)
[ -z "$start" ] && start=1
tail -n +"$start" "$LOG" | grep -aE "error CS[0-9]+" | sed 's/^ *//' | sort -u
exit 1
