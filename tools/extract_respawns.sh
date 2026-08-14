#!/usr/bin/env bash
# Regenerate data/mapwarp_respawns.bin: every safe respawn point (HazardRespawnMarker + TransitionPoint
# respawn) of every room. RespawnPoints embeds and reads the result.
#
# Points are normalized as (pos + offset) / size within the rect the map maps a scene onto. 47 scenes set that rect
# from their _SceneManager FSM (GameMap.SetManualTilemap); for the rest it is the tk2dTileMap size at offset zero.
#
# Layout (little endian): int32 sceneCount, then per scene int32 nameLen, nameLen UTF-8 bytes, four float32
# offsetX, offsetY, width, height, int32 pointCount, and pointCount pairs of float32 x,y. Binary rather than JSON
# because 1.2.2.1 ships no Newtonsoft.Json.
#
# Requires `rabex` (https://github.com/jakobhellermann/rabex-cli) with `references cat --jq` and the
# `world_position` builtin, plus `jq` and `python3`. Run after a game update to refresh the data.
#
#   tools/extract_respawns.sh [steam-game-name]      # default: Hollow Knight
set -euo pipefail

GAME="${1:-Hollow Knight}"
RABEX="${RABEX:-rabex}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/data/mapwarp_respawns.bin"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# The MonoScripts live in globalgamemanagers.assets. Its own file also references them from objects without a
# GameObject (which `go` can't deref), so skip it as a referrer.
ref() {
    "$RABEX" --steam-game "$GAME" file globalgamemanagers.assets object "$1" \
        references --exclude globalgamemanagers cat --jq "$2"
}

echo "extracting (this scans every scene, takes a while)..." >&2
# A marker's world position is its GameObject's Transform world position.
ref HazardRespawnMarker '{scene:._scene, p:(go|transform|world_position)}' | jq -sc . > "$TMP/hrm.json"
# A TransitionPoint's respawn is its respawnMarker's position, or its own when it has none.
ref TransitionPoint '{scene:._scene, p:(if (.respawnMarker.path_id // 0) == 0 then go else (.respawnMarker|deref|go) end | transform | world_position)}' | jq -sc . > "$TMP/tp.json"
# Per-scene size to normalize against (= GameManager.GetSceneWidth/Height).
ref tk2dTileMap '{scene:._scene, w:.width, h:.height}' | jq -sc . > "$TMP/tm.json"
# The scenes whose _SceneManager FSM calls GameMap.SetManualTilemap keep their rect in that FSM's float variables.
ref PlayMakerFSM 'select(tostring | contains("SetManualTilemap")) | {scene:._scene, vars:[.fsm.variables.floatVariables[]? | {(.name): .value}] | add}' | jq -sc . > "$TMP/manual.json"

python3 - "$TMP" "$OUT" <<'PY'
import json, sys, collections
tmp, out = sys.argv[1], sys.argv[2]
hrm = json.load(open(f"{tmp}/hrm.json"))
tp  = json.load(open(f"{tmp}/tp.json"))
dims = {d["scene"]: (d["w"], d["h"]) for d in json.load(open(f"{tmp}/tm.json")) if d.get("scene")}
manual = {m["scene"]: m["vars"] for m in json.load(open(f"{tmp}/manual.json")) if m.get("scene")}

degenerate = []

def rect(scene):
    v = manual.get(scene)
    # Abyss_10 ships all four zeroes, which is no rect the game could divide by either.
    if v and v["Width"] > 0 and v["Height"] > 0:
        return v["Offset X"], v["Offset Y"], v["Width"], v["Height"]
    if v:
        degenerate.append(scene)
    w, h = dims[scene]
    return 0.0, 0.0, float(w), float(h)

pts = collections.defaultdict(list)
for row in hrm + tp:
    if row.get("scene") and row.get("p") is not None:
        pts[row["scene"]].append((row["p"]["x"], row["p"]["y"]))

# Every scene with a tilemap gets an entry, so the rect is there even without respawn points.
data, skipped = {}, 0
for scene in sorted(dims):
    if dims[scene][0] <= 0 or dims[scene][1] <= 0:
        skipped += 1
        continue
    ox, oy, w, h = rect(scene)
    seen, norm = set(), []
    for x, y in pts.get(scene, []):
        key = (round(x), round(y))  # dedup overlapping markers in world space
        if key in seen:
            continue
        seen.add(key)
        norm.append([round((x + ox) / w, 6), round((y + oy) / h, 6)])
    data[scene] = ((ox, oy, w, h), norm)

withpoints = sum(1 for _, n in data.values() if n)
if withpoints < 100:
    raise SystemExit(f"only {withpoints} scenes with points, too few, refusing to write {out}")

import struct
with open(out, "wb") as f:
    f.write(struct.pack("<i", len(data)))
    for scene in sorted(data):
        (ox, oy, w, h), norm = data[scene]
        name = scene.encode("utf-8")
        f.write(struct.pack("<i", len(name)))
        f.write(name)
        f.write(struct.pack("<ffff", ox, oy, w, h))
        f.write(struct.pack("<i", len(norm)))
        for x, y in norm:
            f.write(struct.pack("<ff", x, y))

points = sum(len(n) for _, n in data.values())
print(f"scenes={len(data)} withPoints={withpoints} points={points} "
      f"manualRect={len(manual) - len(degenerate)} degenerateRect={degenerate} "
      f"skipped_no_dims={skipped} -> {out}", file=sys.stderr)
PY
