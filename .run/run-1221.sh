#!/usr/bin/env bash
set -euo pipefail


if [ "$(uname)" = Darwin ]; then
	app="/Users/sipgatejj/Downloads/Hollow Knight-1.2.2.1/hollow_knight.app"
	# app="$HOME/Library/Application Support/Steam/steamapps/common/Hollow Knight/hollow_knight.app"
    macos="$app/Contents/MacOS"
    export SteamAppId=367520 SteamGameId=367520
    cd "$macos"
    exec /usr/bin/arch -x86_64 "$macos/hollow_knight" # run under rosetta
else
    # cd so that steam_appid.txt is respected
    dir="$HOME/.local/share/Steam/steamapps/common/Hollow Knight"
    cd "$dir"
    if [ -x "$dir/run.sh" ]; then
        exec env DISPLAY= "$dir/run.sh"
    else
        exec env DISPLAY= "$dir/hollow_knight.x86_64"
    fi
fi
