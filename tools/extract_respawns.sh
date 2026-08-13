#!/usr/bin/env bash
# Regenerate data/mapwarp_respawns.bin: every safe respawn point (HazardRespawnMarker + TransitionPoint
# respawn) of every room, normalized to [0,1] within its scene. RespawnPoints embeds and reads the result.
#
# Layout (little endian): int32 sceneCount, then per scene int32 nameLen, nameLen UTF-8 bytes, int32 pointCount,
# and pointCount pairs of float32 x,y. Binary rather than JSON because 1.2.2.1 ships no Newtonsoft.Json.
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

python3 - "$TMP" "$OUT" <<'PY'
import json, sys, collections
tmp, out = sys.argv[1], sys.argv[2]
hrm = json.load(open(f"{tmp}/hrm.json"))
tp  = json.load(open(f"{tmp}/tp.json"))
dims = {d["scene"]: (d["w"], d["h"]) for d in json.load(open(f"{tmp}/tm.json")) if d.get("scene")}

pts = collections.defaultdict(list)
for row in hrm + tp:
    if row.get("scene") and row.get("p") is not None:
        pts[row["scene"]].append((row["p"]["x"], row["p"]["y"]))

data, skipped = {}, 0
for scene, world in sorted(pts.items()):
    wh = dims.get(scene)
    if not wh or wh[0] <= 0 or wh[1] <= 0:
        skipped += 1
        continue
    w, h = wh
    seen, norm = set(), []
    for x, y in world:
        key = (round(x), round(y))  # dedup overlapping markers in world space
        if key in seen:
            continue
        seen.add(key)
        norm.append([round(x / w, 6), round(y / h, 6)])
    data[scene] = norm

if len(data) < 100:
    raise SystemExit(f"only {len(data)} scenes with points — that is too few, refusing to write {out}")

import struct
with open(out, "wb") as f:
    f.write(struct.pack("<i", len(data)))
    for scene in sorted(data):
        name = scene.encode("utf-8")
        f.write(struct.pack("<i", len(name)))
        f.write(name)
        f.write(struct.pack("<i", len(data[scene])))
        for x, y in data[scene]:
            f.write(struct.pack("<ff", x, y))

print(f"scenes={len(data)} points={sum(len(v) for v in data.values())} skipped_no_dims={skipped} -> {out}", file=sys.stderr)
PY
