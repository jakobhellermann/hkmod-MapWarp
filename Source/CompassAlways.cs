using System;
using MapWarp.Source.Compat;

namespace MapWarp.Source;

internal static class CompassAlways {
    private static bool positioningCompass;

    internal static void Install() {
        Hooks.Add(typeof(GameMap), nameof(GameMap.PositionCompass),
            (Action<Action<GameMap, bool>, GameMap, bool>)PositionCompass);
        PlayerBoolHook.Add(GetBool);
    }

    internal static void Uninstall() {
        PlayerBoolHook.Remove(GetBool);
    }

    private static void PositionCompass(Action<GameMap, bool> orig, GameMap self, bool posShade) {
        positioningCompass = true;
        try {
            orig(self, posShade);
        } finally {
            positioningCompass = false;
        }
    }

    private static bool GetBool(string name, bool orig) {
        return (MapWarpPlugin.Settings.AlwaysCompass && positioningCompass && name == "equippedCharm_2") || orig;
    }
}
