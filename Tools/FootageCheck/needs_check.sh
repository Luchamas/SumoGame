#!/usr/bin/env bash
# Stop hook: asks Claude for a footage comparison when game code changed since the last one.
cd "$(dirname "$0")/../.." || exit 0

input=$(cat)
[ "$(printf '%s' "$input" | jq -r '.stop_hook_active // false')" = true ] && exit 0
ls Logs/Clips/*.mkv >/dev/null 2>&1 || exit 0

current=$(Tools/FootageCheck/run.sh fingerprint)
checked=$(cat Logs/FootageCheck/last_checked 2>/dev/null)
[ "$current" = "$checked" ] && exit 0

jq -n '{decision: "block", reason: "Game code changed since the last footage comparison. Use the footage-checker agent to compare the game with the clips, then save its report to Logs/FootageCheck/report.md and tell the user what it found. It records the check itself, so this runs once per change."}'
