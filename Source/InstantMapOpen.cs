using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;

namespace MapWarp.Source;

// Make the quick map appear instantly instead of waiting out its open delay. That delay is a single
// Wait(0.2) in the "Quick Map" FSM (_GameCameras/HudCamera/Quick Map), state "Open", between setting up the
// HUD camera and moving on to the map zone. We zero that one action field and restore the authored value when
// the toggle is off. This is the least invasive hook: no global PlayMaker action patch (Wait is shared by
// every FSM in the game) and no per-frame work — PlayMaker never resets action fields at runtime, so setting
// them sticks, and Wait fires FINISHED immediately at 0.
// Apply() is driven by MapLifecycle (on GameMap init) and by the config's SettingChanged.
internal static class InstantMapOpen {
    private static bool captured;
    private static float origWait;

    internal static void Apply() {
        var quickMap = GameCameras.instance.hudCamera.transform.Find("Quick Map");
        var fsm = PlayMakerFSM.FindFsmOnGameObject(quickMap.gameObject, "Quick Map");
        var wait = FindAction<Wait>(fsm, "Open").time;

        // Capture the authored timing once, before we ever overwrite it.
        if (!captured) {
            origWait = wait.Value;
            captured = true;
        }

        wait.Value = MapWarpPlugin.InstantMapOpen.Value ? 0f : origWait;
    }

    private static T FindAction<T>(PlayMakerFSM fsm, string stateName) where T : FsmStateAction =>
        fsm.FsmStates.First(s => s.Name == stateName).Actions.OfType<T>().First();
}
