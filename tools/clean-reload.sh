#!/usr/bin/env bash
# Usage:  tools/clean-reload.sh [slot]      # slot defaults to 1
set -eu

HERE="$(cd "$(dirname "$0")" && pwd)"
LAUNCHER="$HERE/../.run/run-hollow-knight.sh"
SLOT="${1:-1}"
DEV="localhost:8201"

if [ "$(uname)" = Darwin ]; then
    # macOS splits the two logs across different directories.
    PLAYER_LOG="$HOME/Library/Logs/Team Cherry/Hollow Knight/Player.log"
    MODLOG="$HOME/Library/Application Support/unity.Team Cherry.Hollow Knight/ModLog.txt"
    PKILL_PAT="hollow_knight.app/Contents/MacOS/Hollow Knight"
else
    LOGDIR="$HOME/.config/unity3d/Team Cherry/Hollow Knight"
    PLAYER_LOG="$LOGDIR/Player.log"
    MODLOG="$LOGDIR/ModLog.txt"
    PKILL_PAT="hollow_knight.x86_64"
fi

rm -f "$PLAYER_LOG" "$MODLOG"
logsnap open "$PLAYER_LOG" "$MODLOG"
if pkill -f "$PKILL_PAT" 2>/dev/null; then
    echo "killed running Hollow Knight"
    sleep 1
fi

dotnet build

nohup "$LAUNCHER" >/dev/null 2>&1 < /dev/null &
echo "launched Hollow Knight (pid $!)"

echo -n "waiting for debug server"
for _ in $(seq 1 60); do
    if curl -sf --max-time 1 "$DEV/routes" >/dev/null 2>&1; then echo " up"; break; fi
    echo -n "."
    sleep 1
done
logsnap commit -m "loadgame" --settle 200ms

echo "loading save slot $SLOT ..."
curl -s -X POST "$DEV/load-save?slot=$SLOT" -d '' >/dev/null && echo

logsnap commit -m "loadlevel" --wait-for "[HornetSpawner] instantiated" --settle 200ms --at-most 10s

echo "done. checkpoints: loadgame, loadlevel, switch"
