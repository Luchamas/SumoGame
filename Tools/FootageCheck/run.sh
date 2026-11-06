#!/usr/bin/env bash
# Compares the game with the reference footage.
#   run.sh numbers   plays rival matches and checks timing against reference.json
#   run.sh visual    captures game screens and builds side-by-side sheets with the clips
#   run.sh stamp     records that the current code has been checked
set -u
cd "$(dirname "$0")/../.."

UNITY="$HOME/Unity/Hub/Editor/6000.3.10f1/Editor/Unity"
OUT=Logs/FootageCheck
CLIPS=Logs/Clips
GOR=$(ls "$CLIPS"/*2TSFy_wXc1A* 2>/dev/null | head -1)
BO=$(ls "$CLIPS"/*1bUbnvlKHD4* 2>/dev/null | head -1)
mkdir -p "$OUT"

editor_busy() {
    pgrep -x Unity >/dev/null 2>&1
}

numbers() {
    if editor_busy; then
        echo "BLOCKED: Unity already has this project open, so nothing was measured."
        return 3
    fi
    rm -f Temp/UnityLockfile "$OUT/fit.md" "$OUT/numbers.xml"
    timeout 240 "$UNITY" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode \
        -testFilter FootageFitTests -testResults "$OUT/numbers.xml" -logFile "$OUT/numbers.log"
    local result=$?
    if [ ! -f "$OUT/fit.md" ]; then
        echo "FAILED: no report was written. See $OUT/numbers.log"
        return 1
    fi
    cat "$OUT/fit.md"
    return $result
}

# name, clip, seconds into the clip, game screenshot
pair() {
    local name=$1 clip=$2 at=$3 shot=$4
    [ -f "Logs/Shots/$shot.png" ] || { echo "missing game shot $shot"; return; }
    ffmpeg -v error -y -ss "$at" -i "$clip" -frames:v 1 "$OUT/$name-footage.png"
    montage "$OUT/$name-footage.png" "Logs/Shots/$shot.png" -tile 2x1 -geometry 800x450+4+4 "$OUT/$name.jpg"
    rm -f "$OUT/$name-footage.png"
    echo "$OUT/$name.jpg"
}

visual() {
    if [ -z "$GOR" ] || [ -z "$BO" ]; then
        echo "BLOCKED: the clips are missing from $CLIPS."
        return 3
    fi
    if editor_busy; then
        echo "BLOCKED: Unity already has this project open, so no new screens were captured."
        return 3
    fi
    rm -f Temp/UnityLockfile "$OUT/visual.xml"
    rm -rf Logs/Shots
    timeout 480 "$UNITY" -projectPath . -runTests -testPlatform PlayMode \
        -testResults "$OUT/visual.xml" -logFile "$OUT/visual.log" || echo "Some play tests failed. See $OUT/visual.log"
    sheets
}

sheets() {
    pair neutral "$GOR" 102.87 09_single_neutral
    pair grab "$GOR" 104.5 11_single_grab
    pair miss "$GOR" 103.44 13_whiff
    pair drive "$GOR" 110 14_finishing_drive
    pair ringout "$GOR" 122 07_versus_ringout
    pair bo_drive "$BO" 77.33 14_finishing_drive

    exchange grab_vs_grab "$BO" 65.43 1.1
    exchange strike_vs_grab "$BO" 75.4 1.3
    exchange grab_vs_sidewalk "$BO" 66.63 1.1
    exchange grab_break "$BO" 38.56 1.1
}

# name, clip, start in the clip, seconds covered: footage row above the game row
exchange() {
    local name=$1 clip=$2 at=$3 span=$4
    ls "Logs/Shots/ex_${name}_"*.png >/dev/null 2>&1 || { echo "missing game exchange $name"; return; }
    ffmpeg -v error -y -ss "$at" -t "$span" -i "$clip" \
        -vf "fps=8/$span,scale=320:-1,tile=8x1" -frames:v 1 "$OUT/$name-footage.png"
    montage "Logs/Shots/ex_${name}_"*.png -tile 8x1 -geometry 320x180+0+0 "$OUT/$name-game.png"
    magick "$OUT/$name-footage.png" "$OUT/$name-game.png" -append "$OUT/exchange_$name.jpg"
    rm -f "$OUT/$name-footage.png" "$OUT/$name-game.png"
    echo "$OUT/exchange_$name.jpg"
}

fingerprint() {
    { find Assets/Scripts Assets/Editor -type f -name '*.cs' -print0 | sort -z | xargs -0 sha256sum
      sha256sum Tools/FootageCheck/reference.json; } | sha256sum | cut -d' ' -f1
}

case "${1:-}" in
    numbers) numbers ;;
    visual) visual ;;
    sheets) sheets ;;
    stamp) fingerprint > "$OUT/last_checked"; echo "stamped" ;;
    fingerprint) fingerprint ;;
    *) echo "usage: $0 numbers|visual|stamp"; exit 2 ;;
esac
