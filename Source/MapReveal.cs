using System;
using System.Linq;
using UnityEngine;

using MapWarp.Source.Compat;

namespace MapWarp.Source;

internal static class MapReveal {
    private static bool Enabled => MapWarpPlugin.Settings.UnlockEntireMap;

    private static readonly string[] ownedMaps = [
        "hasMap",
        "mapAbyss", "mapCity", "mapCliffs", "mapCrossroads", "mapDeepnest", "mapFogCanyon",
        "mapFungalWastes", "mapGreenpath", "mapMines", "mapOutskirts", "mapRestingGrounds",
        "mapRoyalGardens", "mapWaterways"
    ];

    internal static void Install() {
        PlayerBoolHook.Add(GetBool);

        Hooks.Add(typeof(GameMap), nameof(GameMap.WorldMap), (Action<Action<GameMap>, GameMap>)OnWorldMap);
        foreach (var m in Hooks.Methods(typeof(GameMap), m => m.Name.StartsWith("QuickMap", StringComparison.Ordinal)))
            Hooks.Add(m, (Action<Action<GameMap>, GameMap>)OnQuickMap);
    }

    internal static void Uninstall() {
        PlayerBoolHook.Remove(GetBool);
    }

    private static bool GetBool(string name, bool orig) =>
        (Enabled && ownedMaps.Contains(name)) || orig;

    // Includes the extra sprite pieces of multi-sprite rooms, which need revealing too.
    private static void RevealRooms(GameMap map) {
        foreach (var (_, room) in MapUtil.RoomSprites(map, includeInactive: true)) {
            room.gameObject.SetActive(true);
            ShowFullSprite(room);
        }
    }

    // RoughMapRoom swaps in the full sprite only for mapped rooms and only while becoming active, so a room that
    // is already active would stay a sketch.
    private static void ShowFullSprite(SpriteRenderer room) {
        var rough = room.GetComponent<RoughMapRoom>();
        if (rough == null || rough.fullSpriteDisplayed || rough.fullSprite == null) return;

        room.sprite = rough.fullSprite;
        rough.fullSpriteDisplayed = true;
    }

    private static void OnWorldMap(Action<GameMap> orig, GameMap self) {
        using (Enabled ? MapUnlock.Begin(ownedMaps) : null) orig(self);

        try {
            if (Enabled) RevealRooms(self);
        } catch (Exception e) {
            Logging.Error(e);
        }
    }

    private static void OnQuickMap(Action<GameMap> orig, GameMap self) {
        using (Enabled ? MapUnlock.Begin(ownedMaps) : null) orig(self);

        try {
            if (Enabled) RevealRooms(self);

            if (!MapWarpPlugin.Settings.ShowFullMapInQuickmap) return;

            self.displayNextArea = false;
            foreach (var area in MapUtil.Areas(self))
                area.SetActive(true);

            GameCompat.HideNextAreaDisplays(self);
        } catch (Exception e) {
            Logging.Error(e);
        }
    }
}
