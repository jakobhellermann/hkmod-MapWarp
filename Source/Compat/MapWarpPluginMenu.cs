#if !HK1221
using System;
using System.Collections.Generic;
using Modding;

namespace MapWarp;

// The v42 API of 1.2.2.1 has neither settings persistence nor a mod menu, so there the settings keep their defaults.
public partial class MapWarpPlugin : IGlobalSettings<Settings>, IMenuMod {
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
        Toggle("Always show compass", "Always show your position, as if the Wayward Compass were equipped.",
            () => Settings.AlwaysCompass, v => Settings.AlwaysCompass = v),
        Toggle("Show room borders", "Outline each room on the map and label it with its scene name.",
            () => Settings.ShowRoomBorders, v => Settings.ShowRoomBorders = v)
    ];

    private static IMenuMod.MenuEntry Toggle(string name, string description, Func<bool> get, Action<bool> set) =>
        new(name, ["Off", "On"], description, i => set(i == 1), () => get() ? 1 : 0);
}
#endif
