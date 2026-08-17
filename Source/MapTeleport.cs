using System.Collections.Generic;
using MapWarp.Toasts;
using UnityEngine;
using MapWarp.Compat;

namespace MapWarp;

internal static class MapTeleport {
    internal static void Install() {
        HeroSceneEntry.OnPositioned(OnHeroPositioned);
        HeroSceneEntry.OnEntered(OnSceneEntered);
    }

    // Set on teleport, read after the "dreamGate" transition
    private static string? pendingScene;
    private static Vector2 pendingNormalized;

    // Safe hazard-respawn the last PlaceHero applied, null if no safe spot was found (see OnSceneEntered).
    private static (Vector3 pos, bool facingRight)? lastSafeRespawn;
    private static (Vector3 pos, bool facingRight)? pendingReapplyRespawn;

    internal static string? PreviewRoom;

    // Every loadable room whose box currently contains the cursor (sometimes there's overlap)
    internal static readonly List<(string room, Bounds bounds)> PreviewCandidates = [];

    internal static void ClearPreview() {
        PreviewRoom = null;
        PreviewCandidates.Clear();
    }

    internal static void HandleMap(GameMap gameMap) {
        if (!MapWarpPlugin.Settings.EnableTeleport) {
            ClearPreview();
            return;
        }

        var mapCam = GameCameras.instance.hudCamera;

        var hasRoom = TryGetRoomUnderCursor(gameMap, mapCam, out var best, out var normalized);

        PreviewRoom = hasRoom ? best : null;

        // Left click drag, right click TP
        if (Input.GetMouseButtonDown(1)) {
            if (!hasRoom) {
                ToastManager.Toast("No room under cursor");
                return;
            }

            DoTeleport(best, normalized);
        }
    }

    private static void DoTeleport(string targetScene, Vector2 normalized) {
        LeaveBench();
        CloseInventoryMap();

        var gm = GameManager.instance;
        if (targetScene == gm.sceneName) {
            PlaceHero(RespawnPoints.ToWorld(targetScene, normalized));
            ResumeGameplay();
            return;
        }

        pendingScene = targetScene;
        pendingNormalized = normalized;
        GameCompat.BeginDreamGateTransition(gm, targetScene);
    }

    private static void ResumeGameplay() {
        GameCompat.EndInventoryPause();
    }

    // Re-implementation of bench leaving, without unnecessary delays.
    private static void LeaveBench() {
        var pd = PlayerData.instance;
        if (!pd.atBench) return;

        pd.atBench = false;
        pd.disablePause = false;

        var hero = HeroController.instance;
        hero.GetComponent<Rigidbody2D>().MakeDynamic();
        hero.transform.rotation = Quaternion.identity;
        hero.GetComponent<tk2dSpriteAnimator>().Play("Idle");
        hero.AffectedByGravity(true);
        hero.RegainControl();
        hero.StartAnimationControl();

        // For companions (Grimmchild, etc.)
        PlayMakerFSM.BroadcastEvent("BENCHREST END");
        PlayMakerFSM.BroadcastEvent("BENCH UNSIT");

        var hudCanvas = GameCompat.FindHudCanvas(GameCameras.instance.hudCamera.transform);
        PlayMakerFSM.FindFsmOnGameObject(hudCanvas.gameObject, "Slide Out").SendEvent("IN");

        foreach (var fsm in UnityCompat.FindAll<PlayMakerFSM>())
            if (fsm.FsmName == "Bench Control" && fsm.ActiveStateName != "Idle")
                fsm.SetState("Reactivate");
    }

    private static void CloseInventoryMap() {
        if (!GameCameras.instance) return;
        var hud = GameCameras.instance.hudCamera.transform;

        var quickMap = hud.Find("Quick Map");
        if (quickMap)
            PlayMakerFSM.FindFsmOnGameObject(quickMap.gameObject, "Quick Map").SendEvent("CLOSE QUICK MAP");

        var inventory = hud.Find("Inventory");
        if (!inventory) return;

        // Zoomed into a scene map, the game closes via the World Map's own INVENTORY CANCEL state, which
        // restores the wide map pose before forwarding CLOSE to the inventory.
        var worldMap = inventory.Find("Map/World Map");
        if (worldMap &&
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

    private static bool TryGetRoomUnderCursor(GameMap gameMap, Camera mapCam, out string best, out Vector2 normalized) {
        best = null!;
        normalized = default;
        PreviewCandidates.Clear();
        var mouse = (Vector2)Input.mousePosition;
        var bestDist = float.MaxValue;
        Bounds bestBounds = default;

        foreach (var (name, sr) in MapUtil.Rooms(gameMap, false)) {
            if (!sr.sprite) continue;

            var screenMin = MapUtil.WorldToScreen(mapCam, sr.bounds.min);
            var screenMax = MapUtil.WorldToScreen(mapCam, sr.bounds.max);
            float xMin = Mathf.Min(screenMin.x, screenMax.x), xMax = Mathf.Max(screenMin.x, screenMax.x);
            float yMin = Mathf.Min(screenMin.y, screenMax.y), yMax = Mathf.Max(screenMin.y, screenMax.y);

            if (mouse.x < xMin || mouse.x > xMax) continue;
            if (mouse.y < yMin || mouse.y > yMax) continue;

            PreviewCandidates.Add((name, sr.bounds));

            var dist = SceneCursorScore(mapCam, name, sr.bounds, mouse);
            if (dist < bestDist) {
                bestDist = dist;
                best = name;
                bestBounds = sr.bounds;
            }
        }

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (bestDist == float.MaxValue) return false;

        var bestScreenMin = MapUtil.WorldToScreen(mapCam, bestBounds.min);
        var bestScreenMax = MapUtil.WorldToScreen(mapCam, bestBounds.max);
        normalized = new Vector2(
            Mathf.Clamp01((mouse.x - bestScreenMin.x) / (bestScreenMax.x - bestScreenMin.x)),
            Mathf.Clamp01((mouse.y - bestScreenMin.y) / (bestScreenMax.y - bestScreenMin.y)));
        return true;
    }

    // Squared screen-pixel distance from the cursor to the scene's nearest respawn point; disambiguates
    // overlapping room boxes better than the box center, which is the fallback for scenes without points.
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
        if (pendingScene is not { } scene) return;
        pendingScene = null;
        PlaceHero(RespawnPoints.ToWorld(scene, pendingNormalized));

        // FinishedEnteringScene still runs after this and re-anchors the hazard respawn; queue the safe spot
        // for OnSceneEntered.
        pendingReapplyRespawn = lastSafeRespawn;
    }

    // FinishedEnteringScene can't resolve the "dreamGate" gate and anchors the hazard respawn at the landing
    // position; landing inside a hazard would then respawn-loop. Postfix, so this wins.
    private static void OnSceneEntered(HeroController self) {
        if (pendingReapplyRespawn is not { } respawn) return;
        pendingReapplyRespawn = null;
        self.SetHazardRespawn(respawn.pos, respawn.facingRight);
        ResumeGameplay();
    }

    private static void PlaceHero(Vector2 target) {
        var hasSafeSpot = TryFindNearestSafeSpot(target, out var safeSpot);

        // Anchor the hazard respawn before the hero can touch anything lethal (see OnSceneEntered).
        var hero = HeroController.instance;
        if (hasSafeSpot) {
            hero.SetHazardRespawn(safeSpot, hero.cState.facingRight);
            lastSafeRespawn = (safeSpot, hero.cState.facingRight);
        } else {
            lastSafeRespawn = null;
        }

        // Vanilla chooses the ground point as well
        var ground = hero.FindGroundPoint(hasSafeSpot ? safeSpot : target, true);
        hero.transform.position = new Vector3(ground.x, ground.y, hero.transform.position.z);
    }

    private static bool TryFindNearestSafeSpot(Vector2 target, out Vector3 safeSpot) {
        var bestDist = float.MaxValue;
        safeSpot = Vector3.zero;
        var found = false;

        foreach (var m in UnityCompat.FindAll<HazardRespawnMarker>()) {
            var d = ((Vector2)m.transform.position - target).sqrMagnitude;
            if (d < bestDist) (bestDist, safeSpot, found) = (d, m.transform.position, true);
        }

        foreach (var tp in UnityCompat.FindAll<TransitionPoint>()) {
            var pos = tp.respawnMarker ? tp.respawnMarker.transform.position : tp.transform.position;
            var d = ((Vector2)pos - target).sqrMagnitude;
            if (d < bestDist) (bestDist, safeSpot, found) = (d, pos, true);
        }

        return found;
    }
}
