using System;

namespace MapWarp.Source;

// Central "the GameMap (re)initialised" hook. Hooked once on GameMap.Start and GameMap.OnEnable, then
// dispatched to every feature that needs to (re)install itself when a map appears — on scene entry, a fresh
// game load, or a hot reload. Features add their init call to Dispatch() here instead of each declaring its
// own GameMap lifecycle hook. Each call is isolated so one failing feature doesn't skip the rest.
internal static class MapLifecycle {
    internal static void Install() {
        Hooks.Add(typeof(GameMap), "Start", (Action<Action<GameMap>, GameMap>)OnStart);
        Hooks.Add(typeof(GameMap), "OnEnable", (Action<Action<GameMap>, GameMap>)OnEnable);
    }

    private static void OnStart(Action<GameMap> orig, GameMap self) {
        orig(self);
        Dispatch();
    }

    private static void OnEnable(Action<GameMap> orig, GameMap self) {
        orig(self);
        Dispatch();
    }

    // Also called directly from the plugin's Initialize so a hot reload (GameMap already present, so the hooks
    // above won't fire) still initialises. Every handler is a no-op when no map is present.
    internal static void Dispatch() {
        Run(MapRoomBorders.Install);
    }

    private static void Run(Action handler) {
        try {
            handler();
        } catch (Exception e) {
            Logging.Error(e);
        }
    }
}
