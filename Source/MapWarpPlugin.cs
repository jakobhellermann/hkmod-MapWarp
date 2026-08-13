using System;
using System.Collections.Generic;
using System.Reflection;
using MapWarp.Source.Toasts;
using Modding;
using UnityEngine;
using Object = UnityEngine.Object;

using MapWarp.Source.Compat;

namespace MapWarp.Source;

public class MapWarpPlugin() : Mod("MapWarp"), ITogglableMod, IGlobalSettings<Settings>, IMenuMod {
    internal static Settings Settings = new();

    public override string GetVersion() => Assembly.GetExecutingAssembly().GetName().Version.ToString();

    public void OnLoadGlobal(Settings s) => Settings = s;

    public Settings OnSaveGlobal() => Settings;

    public bool ToggleButtonInsideMenu => false;

    public List<IMenuMod.MenuEntry> GetMenuData(IMenuMod.MenuEntry? toggleButtonEntry) => [
        Toggle("Enable teleport", "Right-click a room on the map to warp to the nearest safe spot.",
            () => Settings.EnableTeleport, v => Settings.EnableTeleport = v),
        Toggle("Show respawn points", "When hovering a room, mark its transition / hazard-respawn spots.",
            () => Settings.ShowRespawnPoints, v => Settings.ShowRespawnPoints = v),
        Toggle("Unlock entire map", "Show the whole map even in zones you haven't acquired it for.",
            () => Settings.UnlockEntireMap, v => Settings.UnlockEntireMap = v),
        Toggle("Full map in quickmap", "Show every zone in the quick map instead of only the current one.",
            () => Settings.ShowFullMapInQuickmap, v => Settings.ShowFullMapInQuickmap = v),
        Toggle("Instant map open", "Open the quick map without its opening delay.",
            () => Settings.InstantMapOpen, v => {
                Settings.InstantMapOpen = v;
                InstantMapOpen.Apply();
            }),
        Toggle("Always show compass", "Always show your position, as if the Wayward Compass were equipped.",
            () => Settings.AlwaysCompass, v => Settings.AlwaysCompass = v),
        Toggle("Show room borders", "Outline each room on the map and label it with its scene name.",
            () => Settings.ShowRoomBorders, v => Settings.ShowRoomBorders = v)
    ];

    private static IMenuMod.MenuEntry Toggle(string name, string description, Func<bool> get, Action<bool> set) =>
        new(name, ["Off", "On"], description, i => set(i == 1), () => get() ? 1 : 0);

    public override void Initialize() {
        base.Initialize();

        Logging.Init(this);
        Logging.Info($"Plugin {Name} has loaded!");

        MapLifecycle.Install();
        MapTeleport.Install();
        MapReveal.Install();
        CompassAlways.Install();
        MapNavigationCursor.Install();
        ToastManager.Install();

        // Hot reload: the GameMap may already exist when the plugin (re)loads, so MapLifecycle's Start/
        // OnEnable hooks won't fire. Dispatch directly (each handler is a no-op when no map is present).
        MapLifecycle.Dispatch();
    }

    public void Unload() {
        // Clean up everything, in order to support hot reloading
        Hooks.UninstallAll();
        MapReveal.Uninstall();
        MapNavigationCursor.Uninstall();
        CompassAlways.Uninstall();

        foreach (var c in UnityCompat.FindAll<MapRoomBorders>(includeInactive: true))
            Object.Destroy(c);
        foreach (var c in UnityCompat.FindAll<MapNavigation>(includeInactive: true))
            Object.Destroy(c);
        foreach (var c in UnityCompat.FindAll<ToastManager>(includeInactive: true))
            Object.Destroy(c.gameObject);

        Logging.Info($"Plugin {Name} has been unloaded!");
    }
}
