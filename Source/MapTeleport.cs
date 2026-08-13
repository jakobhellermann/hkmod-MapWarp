using System;
using System.Collections.Generic;
using MapWarp.Source.Toasts;
using GlobalEnums;
using Modding;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

using MapWarp.Source.Compat;

namespace MapWarp.Source;

internal static class MapTeleport {
    internal static void Install() {
        Hooks.Add(typeof(GameMap), "Update", (Action<Action<GameMap>, GameMap>)GameMapUpdate);
        HeroSceneEntry.OnPositioned(OnHeroPositioned);
        HeroSceneEntry.OnEntered(OnSceneEntered);
    }

    private static float MapSceneWidth(GameMap map) => Reflect.GetField<GameMap, float>(map, "sceneWidth");
    private static float MapSceneHeight(GameMap map) => Reflect.GetField<GameMap, float>(map, "sceneHeight");

    // Cross-scene teleports go through a "dreamGate" transition. The destination position isn't known until the new
    // scene's tilemap is loaded, so we stash the click's normalized room position here and resolve it to world
    // coordinates in the OnHeroPositioned postfix once GetSceneWidth/Height are valid for the destination.
    private static bool pendingDreamGate;
    private static Vector2 pendingNormalized;

    // Safe hazard-respawn the last PlaceHero applied (null if no safe spot was found). For a cross-scene teleport
    // OnHeroPositioned promotes this into pendingReapplyRespawn so it can be re-applied after
    // FinishedEnteringScene re-anchors the respawn to the hero's landing position (see OnSceneEntered).
    private static (Vector3 pos, bool facingRight)? lastSafeRespawn;
    private static (Vector3 pos, bool facingRight)? pendingReapplyRespawn;

    // The room (loadable scene) currently under the cursor, updated every frame the map is open and drawn next
    // to the cursor by MapNavigation.OnGUI. Null when no map is open / no room is hovered.
    internal static string? PreviewRoom;

    // Every loadable room whose box currently contains the cursor, with its map-sprite bounds — usually one, but
    // several where room boxes overlap. MapNavigation draws the respawn points of all of them (not just the
    // selected room), so you can see every safe spot at an overlap. Reused list, refilled each frame.
    internal static readonly List<(string room, Bounds bounds)> PreviewCandidates = new();

    internal static bool IsLoadableScene(string sceneName) => UnityCompat.IsLoadableScene(sceneName);

    // The live GameMap, refreshed each frame from its own Update; read by MapNavigation.
    internal static GameMap? Current;

    private static void GameMapUpdate(Action<GameMap> orig, GameMap self) {
        orig(self);

        // Runs every frame on the game's GameMap.Update — guard so an exception can't break it.
        try {
            Current = self;
            HandleMap(self);
        } catch (Exception e) {
            ClearPreview();
            Logging.Error(e);
        }
    }

    // Reset the per-frame cursor preview state (no map open / no room hovered).
    private static void ClearPreview() {
        PreviewRoom = null;
        PreviewCandidates.Clear();
    }

    private static void HandleMap(GameMap gameMap) {
        if (!MapWarpPlugin.Settings.EnableTeleport) {
            ClearPreview();
            return;
        }

        // The GameMap object stays active while no map is shown, so the active areas gate "a map is open".
        if (!MapUtil.AnyAreaActive(gameMap)) {
            ClearPreview();
            return;
        }

        var mapCam = GameCameras.instance.hudCamera;

        var gm = GameManager.instance;
        var hasRoom = TryGetRoomUnderCursor(mapCam, out var best, out var normalized);

        // Update the cursor preview every frame (drawn by MapNavigation.OnGUI).
        PreviewRoom = hasRoom ? best : null;

        // Left mouse is used for drag-panning (MapNavigation), so teleport is bound to a discrete right-click.
        if (!Input.GetMouseButtonDown(1)) return;


        if (!hasRoom) {
            ToastManager.Toast("No room under cursor");
            return;
        }

        LeaveBench();

        var targetScene = best;
        if (targetScene == gm.sceneName) {
            CloseInventoryMap();
            PlaceHero(new Vector2(normalized.x * MapSceneWidth(gameMap), normalized.y * MapSceneHeight(gameMap)));
            ResumeGameplay();
            return;
        }

        CloseInventoryMap();

        pendingDreamGate = true;
        pendingNormalized = normalized;
        GameCompat.BeginDreamGateTransition(gm, targetScene);
    }

    private static void ResumeGameplay() {
        GameCompat.EndInventoryPause();
    }

    // Nothing gets the hero off a bench when we warp him away from it: the bench is scene-local, so its
    // Bench Control FSM dies with the scene. Driving that FSM instead is not an option either — its Get Off
    // path spans several frames and its Game Paused? state cancels back to Resting while the inventory is
    // still open. So replicate Get Off / Idle Pause / Regain Control here.
    private static void LeaveBench() {
        var pd = PlayerData.instance;
        if (!pd.GetBool("atBench")) return;

        pd.SetBool("atBench", false);
        pd.SetBool("disablePause", false);

        var hero = HeroController.instance;
        hero.GetComponent<Rigidbody2D>().MakeDynamic();
        hero.transform.rotation = Quaternion.identity;
        hero.GetComponent<tk2dSpriteAnimator>().Play("Idle");
        hero.AffectedByGravity(true);
        hero.RegainControl();
        hero.StartAnimationControl();

        // Companions (Grimmchild, Weaverling, Knight Hatchling) and the bench's own sit animation listen for these.
        PlayMakerFSM.BroadcastEvent("BENCHREST END");
        PlayMakerFSM.BroadcastEvent("BENCH UNSIT");

        // Resting slides the HUD out; Get Off slides it back in.
        var hudCanvas = GameCameras.instance.hudCamera.transform.Find("Anchor TL/Hud Canvas Offset/Hud Canvas");
        PlayMakerFSM.FindFsmOnGameObject(hudCanvas.gameObject, "Slide Out")?.SendEvent("IN");

        // A same-scene teleport leaves the bench's own FSM sitting in Resting / Map Idle, and closing the quick
        // map afterwards walks it back into Resting, which re-sets atBench. Park it in the state Get Off would
        // have reached: Reactivate restores the bench particles and falls through to Idle.
        foreach (var fsm in UnityCompat.FindAll<PlayMakerFSM>())
            if (fsm.FsmName == "Bench Control" && fsm.ActiveStateName != "Idle")
                fsm.SetState("Reactivate");
    }

    private static void CloseInventoryMap() {
        if (GameCameras.instance == null) return;
        var hud = GameCameras.instance.hudCamera.transform;

        var quickMap = hud.Find("Quick Map");
        if (quickMap != null)
            PlayMakerFSM.FindFsmOnGameObject(quickMap.gameObject, "Quick Map")?.SendEvent("CLOSE QUICK MAP");

        var inventory = hud.Find("Inventory");
        if (inventory == null) return;

        // Zoomed into a scene map, the game closes via the World Map's own INVENTORY CANCEL state, which
        // restores the wide map pose before forwarding CLOSE to the inventory.
        var worldMap = inventory.Find("Map/World Map");
        if (worldMap != null &&
            PlayMakerFSM.FindFsmOnGameObject(worldMap.gameObject, "UI Control") is
                { ActiveStateName: "Zoomed In" } uiControl) {
            uiControl.SendEvent("INVENTORY CANCEL");
            return;
        }

        // CLOSE, not INVENTORY CANCEL: the latter is the roar-lock path and never calls RegainControl.
        // Can Close? bails while Do Not Close is set.
        if (PlayMakerFSM.FindFsmOnGameObject(inventory.gameObject, "Inventory Control") is { } inventoryControl) {
            inventoryControl.FsmVariables.GetFsmBool("Do Not Close").Value = false;
            inventoryControl.SendEvent("CLOSE");
        }
    }

    // Map room whose on-screen sprite bounds contain the cursor; when several overlap, the one whose nearest
    // respawn point is closest to the cursor wins (see SceneCursorScore). Also returns the cursor's normalized
    // [0,1] position within that room.
    private static bool TryGetRoomUnderCursor(Camera mapCam, out string best, out Vector2 normalized) {
        best = null!;
        normalized = default;
        PreviewCandidates.Clear();
        var mouse = (Vector2)Input.mousePosition;
        var bestDist = float.MaxValue;
        Bounds bestBounds = default;

        foreach (var (name, sr) in MapUtil.Rooms(Current!, false)) {
            if (sr.sprite == null) continue;

            // Letterbox-corrected on-screen pixels (bottom-left, matching Input.mousePosition) — plain
            // WorldToScreenPoint returns render-texture pixels and misaligns under black bars (see MapUtil).
            var smin = MapUtil.WorldToScreen(mapCam, sr.bounds.min);
            var smax = MapUtil.WorldToScreen(mapCam, sr.bounds.max);
            float xMin = Mathf.Min(smin.x, smax.x), xMax = Mathf.Max(smin.x, smax.x);
            float yMin = Mathf.Min(smin.y, smax.y), yMax = Mathf.Max(smin.y, smax.y);

            if (mouse.x < xMin || mouse.x > xMax) continue;
            if (mouse.y < yMin || mouse.y > yMax) continue;

            // Room objects that aren't loadable scenes themselves (area labels, pin containers) aren't targets.
            if (!IsLoadableScene(name)) continue;

            PreviewCandidates.Add((name, sr.bounds));

            // Among overlapping matches, pick the one whose content is nearest the cursor (SceneCursorScore).
            var dist = SceneCursorScore(mapCam, name, sr.bounds, mouse);
            if (dist < bestDist) {
                bestDist = dist;
                best = name;
                bestBounds = sr.bounds;
            }
        }

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (bestDist == float.MaxValue) return false;

        var bsmin = MapUtil.WorldToScreen(mapCam, bestBounds.min);
        var bsmax = MapUtil.WorldToScreen(mapCam, bestBounds.max);
        normalized = new Vector2(
            Mathf.Clamp01((mouse.x - bsmin.x) / (bsmax.x - bsmin.x)),
            Mathf.Clamp01((mouse.y - bsmin.y) / (bsmax.y - bsmin.y)));
        return true;
    }

    // Screen-space (squared) distance from the cursor to the scene's nearest respawn point — concrete in-room
    // locations, so a smaller value means the cursor is over that scene's actual content. This disambiguates
    // overlapping room boxes better than the box center. Falls back to the box-center distance for scenes with
    // no respawn points; both are screen-pixel sqrMagnitudes, so they're comparable across scenes.
    private static float SceneCursorScore(Camera mapCam, string scene, Bounds worldBounds, Vector2 mouse) {
        var points = RespawnPoints.Get(scene);
        if (points == null || points.Count == 0)
            return (MapUtil.WorldToScreen(mapCam, worldBounds.center) - mouse).sqrMagnitude;

        var best = float.MaxValue;
        foreach (var p in points) {
            var world = new Vector3(worldBounds.min.x + p.x * worldBounds.size.x,
                worldBounds.min.y + p.y * worldBounds.size.y, worldBounds.center.z);
            var d = (MapUtil.WorldToScreen(mapCam, world) - mouse).sqrMagnitude;
            if (d < best) best = d;
        }

        return best;
    }

    // For "dreamGate" the game's FindEntryPoint returns the stored dream gate position, so it lands somewhere
    // unrelated. We override the final position here, only for teleports we initiated.
    private static void OnHeroPositioned(GameManager self) {
        if (!pendingDreamGate) return;
        pendingDreamGate = false;
        PlaceHero(new Vector2(pendingNormalized.x * self.GetSceneWidth(),
            pendingNormalized.y * self.GetSceneHeight()));

        // A cross-scene teleport still runs FinishedEnteringScene after this, which re-anchors the hazard respawn to
        // the hero's (possibly hazardous) landing position because it can't resolve the "dreamGate" entry gate.
        // Queue the safe spot to be re-applied after that (OnSceneEntered).
        pendingReapplyRespawn = lastSafeRespawn;
    }

    // Re-apply the safe hazard respawn after a cross-scene teleport. FinishedEnteringScene (run after
    // the hero was positioned) sets the respawn to the hero's landing position when the entry gate is
    // unresolved ("dreamGate"); if that landing is inside a hazard, the accepted single death would respawn back
    // into it and loop, each respawn forcing a full blocking GC → the game grinds to ~1 fps. Overriding it here
    // (postfix, so after that assignment) points the respawn at a known-safe spot instead.
    private static void OnSceneEntered(HeroController self) {
        if (pendingReapplyRespawn is not { } respawn) return;
        pendingReapplyRespawn = null;
        self.SetHazardRespawn(respawn.pos, respawn.facingRight);
        ResumeGameplay();
    }

    // Place the hero for a teleport: snap to the nearest guaranteed-safe spot (a transition point or
    // hazard-respawn marker) near the target, or ground-snap the target when the scene has none.
    private static void PlaceHero(Vector2 target) {
        var hasSafeSpot = TryFindNearestSafeSpot(target, out var safeSpot);

        // Anchor the hazard-respawn location to a known-safe spot before the hero can touch anything lethal, so a
        // hazard death recovers after one respawn instead of looping (see OnSceneEntered for why a
        // cross-scene teleport also needs a re-apply after FinishedEnteringScene).
        var hero = HeroController.instance;
        if (hasSafeSpot) {
            hero.SetHazardRespawn(safeSpot, hero.cState.facingRight);
            lastSafeRespawn = (safeSpot, hero.cState.facingRight);
        } else {
            lastSafeRespawn = null;
        }

        // A raw normalized position almost always lands inside terrain. FindGroundPoint is the game's own
        // ground-snap: it raycasts down onto the terrain and accounts for the hero's collider height.
        // useExtended searches the full scene height, so the click drops onto the floor beneath it.
        var ground = hero.FindGroundPoint(hasSafeSpot ? safeSpot : target, true);
        hero.transform.position = new Vector3(ground.x, ground.y, hero.transform.position.z);
    }

    // Nearest transition / hazard-respawn marker to `target`, in the currently loaded scene — both are spots
    // the game itself spawns the hero at, so they're always on safe ground.
    private static bool TryFindNearestSafeSpot(Vector2 target, out Vector3 safeSpot) {
        var bestDist = float.MaxValue;
        safeSpot = Vector3.zero;
        var found = false;

        foreach (var m in UnityCompat.FindAll<HazardRespawnMarker>()) {
            var d = ((Vector2)m.transform.position - target).sqrMagnitude;
            if (d < bestDist) (bestDist, safeSpot, found) = (d, m.transform.position, true);
        }

        foreach (var tp in UnityCompat.FindAll<TransitionPoint>()) {
            var pos = tp.respawnMarker != null ? tp.respawnMarker.transform.position : tp.transform.position;
            var d = ((Vector2)pos - target).sqrMagnitude;
            if (d < bestDist) (bestDist, safeSpot, found) = (d, pos, true);
        }

        return found;
    }
}
