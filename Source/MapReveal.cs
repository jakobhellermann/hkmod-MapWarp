using System;
using Modding;
using UnityEngine;

namespace MapWarp.Source;

internal static class MapReveal {
    private static bool Enabled => MapWarpPlugin.Settings.UnlockEntireMap;

    private static readonly string[] UnlockBools = [
        "hasMap", "hasQuill", "mapAllRooms",
        "mapAbyss", "mapCity", "mapCliffs", "mapCrossroads", "mapDeepnest", "mapFogCanyon",
        "mapFungalWastes", "mapGreenpath", "mapMines", "mapOutskirts", "mapRestingGrounds",
        "mapRoyalGardens", "mapWaterways"
    ];

    internal static void Install() {
        ModHooks.GetPlayerBoolHook += GetBool;

        Hooks.Add(typeof(GameMap), nameof(GameMap.WorldMap), (Action<Action<GameMap>, GameMap>)OnWorldMap);
        foreach (var m in Hooks.Methods(typeof(GameMap), m => m.Name.StartsWith("QuickMap", StringComparison.Ordinal)))
            Hooks.Add(m, (Action<Action<GameMap>, GameMap>)OnQuickMap);
    }

    internal static void Uninstall() {
        ModHooks.GetPlayerBoolHook -= GetBool;
    }

    private static bool GetBool(string name, bool orig) =>
        (Enabled && Array.IndexOf(UnlockBools, name) >= 0) || orig;

    private static void OnWorldMap(Action<GameMap> orig, GameMap self) {
        orig(self);

        try {
            if (Enabled) self.SetupMap();
        } catch (Exception e) {
            Logging.Error(e);
        }
    }

    private static void OnQuickMap(Action<GameMap> orig, GameMap self) {
        orig(self);

        try {
            if (Enabled) self.SetupMap();

            if (!MapWarpPlugin.Settings.ShowFullMapInQuickmap) return;

            self.displayNextArea = false;
            foreach (var area in MapUtil.Areas(self))
                area.SetActive(true);

            foreach (var display in self.GetComponentsInChildren<MapNextAreaDisplay>(true))
                foreach (Transform child in display.transform)
                    child.gameObject.SetActive(false);
        } catch (Exception e) {
            Logging.Error(e);
        }
    }
}
