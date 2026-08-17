using System;
using UnityEngine;

using MapWarp.Source.Compat;

namespace MapWarp.Source;

internal static class MapLifecycle {
    internal static GameMap? Current;
    internal static bool MapOpen;

    internal static void Install() {
        Hooks.Add(typeof(GameMap), "Start", (Action<Action<GameMap>, GameMap>)OnStart);
        Hooks.Add(typeof(GameMap), "OnEnable", (Action<Action<GameMap>, GameMap>)OnEnable);
    }

    private static void OnStart(Action<GameMap> orig, GameMap self) {
        orig(self);
        Current = self;
        Dispatch();
    }

    private static void OnEnable(Action<GameMap> orig, GameMap self) {
        orig(self);
        Current = self;
        Dispatch();
    }

    internal static void Dispatch() {
        try {
            // A map created before the plugin initialized never went through the hooks above.
            if (!Current) Current = UnityCompat.FindFirst<GameMap>();
            MapRoomBorders.Rebuild();
            UpdateDriver.Install();
        } catch (Exception e) {
            Logging.Error(e);
        }
    }
}

[RequireComponent(typeof(Camera))]
internal sealed class UpdateDriver : MonoBehaviour {
    private Camera cam = null!;

    private void Awake() {
        cam = GetComponent<Camera>();
    }

    private void Update() {
        try {
            var map = MapLifecycle.Current;
            if (map == null || !MapUtil.AnyAreaActive(map)) {
                MapLifecycle.MapOpen = false;
                MapTeleport.ClearPreview();
                return;
            }

            MapLifecycle.MapOpen = true;
            MapPanZoom.HandleFrame(map, cam);
            MapTeleport.HandleMap(map);
        } catch (Exception e) {
            Logging.Error(e);
        }
    }

    private void OnDisable() => MapLifecycle.MapOpen = false;

    private void OnGUI() {
        try {
            if (!MapLifecycle.MapOpen) return;
            MapRoomBorders.DrawLabels(cam);
            MapPanZoom.DrawPreview(cam);
        } catch (Exception e) {
            Logging.Error(e);
        }
    }

    private void OnPostRender() {
        try {
            if (!MapLifecycle.MapOpen) return;
            MapRoomBorders.DrawBorders(cam);
        } catch (Exception e) {
            Logging.Error(e);
        }
    }

    internal static void Install() {
        // Dispatch runs on every map (re)init; replace instead of stacking a second driver.
        foreach (var old in UnityCompat.FindAll<UpdateDriver>(includeInactive: true))
            Destroy(old);
        // Null until _GameCameras awoke; the next GameMap lifecycle event retries.
        var cam = GameCameras.instance ? GameCameras.instance.hudCamera : null;
        if (cam == null) return;
        cam.gameObject.AddComponent<UpdateDriver>();
    }
}
