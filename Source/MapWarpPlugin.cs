using System;
using System.Reflection;
using MapWarp.Source.Polyfill;
using MapWarp.Source.Toasts;
using Modding;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MapWarp.Source;

public class MapWarpPlugin() : Mod("MapWarp"), ITogglableMod {
    internal static ConfigEntry<bool> EnableTeleport = null!;
    internal static ConfigEntry<bool> ShowRoomBorders = null!;
    internal static ConfigEntry<bool> ShowFullMapInQuickmap = null!;
    internal static ConfigEntry<bool> UnlockEntireMap = null!;
    internal static ConfigEntry<bool> InstantMapOpen = null!;
    internal static ConfigEntry<bool> ShowRespawnPoints = null!;
    internal static ConfigEntry<bool> AlwaysCompass = null!;

    public override string GetVersion() => Assembly.GetExecutingAssembly().GetName().Version.ToString();

    public override void Initialize() {
        base.Initialize();
        
        Logging.Init(this);
        Logging.Info($"Plugin {Name} has loaded!");

        try {
            EnableTeleport = Config.Bind("Teleport", "Enable teleport", true,
                "Right-click a room on the map to warp to the the nearest safe spot (hold Shift exact spot.)");
            UnlockEntireMap = Config.Bind("Map", "Unlock entire map", true,
                "Open and pan the whole map even in zones you haven't acquired it for.");
            ShowFullMapInQuickmap = Config.Bind("Map", "Show full map in quickmap", false,
                "Show the entire map instead of the current area in quickmap");
            InstantMapOpen = Config.Bind("Map", "Instant map open", true,
                "Open the quick map instantly instead of waiting for the open animation.");
            InstantMapOpen.SettingChanged += (_, _) => MapWarp.Source.InstantMapOpen.Apply();
            ShowRoomBorders = Config.Bind("Debug", "Show Room Borders", false,
                "Outline each room on the map and label it with its scene name.");
            ShowRespawnPoints = Config.Bind("Teleport", "Show respawn points", true,
                "When hovering a room on the map, mark its safe respawn points (transition / hazard-respawn spots).");
            AlwaysCompass = Config.Bind("Map", "Always show compass", false,
                "Always show your position on the map, as if the Compass tool were equipped.");

            MapLifecycle.Install();
            MapTeleport.Install();
            MapReveal.Install();
            CompassAlways.Install();
            MapNavigationCursor.Install();
            ToastManager.Install();

            // Hot reload: the GameMap may already exist when the plugin (re)loads, so MapLifecycle's Start/
            // OnEnable hooks won't fire. Dispatch directly (each handler is a no-op when no map is present).
            MapLifecycle.Dispatch();
        } catch (Exception e) {
            Logging.Info($"Plugin {Name} failed to initialize: {e}");
        }
    }

    public void Unload() {
        // Clean up everything, in order to support hot reloading

        try {
            Hooks.UninstallAll();
            MapReveal.Uninstall();
            CompassAlways.Uninstall();

            foreach (var c in Object.FindObjectsByType<MapRoomBorders>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(c);
            foreach (var c in Object.FindObjectsByType<MapNavigation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(c);
            foreach (var c in Object.FindObjectsByType<ToastManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(c.gameObject);
        } catch (Exception e) {
            Logging.Info($"Plugin {Name} failed to clean up: {e}");
        }

        Logging.Info($"Plugin {Name} has been unloaded!");
    }
}
