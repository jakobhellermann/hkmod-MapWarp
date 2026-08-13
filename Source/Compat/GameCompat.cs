using GlobalEnums;
using UnityEngine;

namespace MapWarp.Source.Compat;

internal static class GameCompat {
    /// Undoes what opening the inventory did to the running game. Only 1.5.12620 pauses and freezes there, so on
    /// older versions there is nothing to undo.
    internal static void EndInventoryPause() {
#if HK1512620
        // SetIsInventoryOpen(true) -> SetPausedState(true) -> SetTimeScale(0). The FSM undoes that in Regain Control,
        // but forcing the close and starting the scene transition in the same frame loses it and the world stays
        // frozen, so do it ourselves once the teleport has landed.
        GameManager.instance.SetIsInventoryOpen(false);

        // Opening the inventory renders the world into a texture, disables the camera and shows the texture on the
        // ScreenPlane. Unfreeze re-enables the camera and queues the plane's renderer off; without it the world keeps
        // simulating behind a frozen screenshot. Unfreeze is a no-op when nothing is frozen.
        var screenPlane = GameCameras.instance.hudCamera.transform.Find("Inventory/Border/Inventory ScreenPlane");
        screenPlane.GetComponent<DisplayFrozenCamera>().Unfreeze();
#endif
    }

    internal static void BeginDreamGateTransition(GameManager gm, string targetScene) {
#if HK1221
        // 1.2.2.1 has no SceneLoadInfo; ChangeToScene is what WarpToDreamGate itself goes through.
        gm.ChangeToScene(targetScene, "dreamGate", 0f);
#else
        // PreventCameraFadeOut: the dreamGate entry path never sends "SCENE FADE IN", so allowing the fade-out would
        // leave the screen black. Suppressing it (a hard cut) matches what DebugMod/PreciseSavestates do for dreamGate.
        gm.BeginSceneTransition(new GameManager.SceneLoadInfo {
            SceneName = targetScene,
            HeroLeaveDirection = GatePosition.unknown,
            EntryGateName = "dreamGate",
            EntryDelay = 0f,
            PreventCameraFadeOut = true,
            WaitForSceneTransitionCameraFade = false
        });
#endif
    }

    /// 1.2.2.1 has no next-area name plates on the quick map.
    internal static void HideNextAreaDisplays(GameMap map) {
#if !HK1221
        foreach (var display in map.GetComponentsInChildren<MapNextAreaDisplay>(true))
            foreach (Transform child in display.transform)
                child.gameObject.SetActive(false);
#endif
    }
}

#if !HK1512620
internal static class TransformCompat {
    /// The game ships this from 1.5.12620 on: Extensions.SetLocalPosition2D
    internal static void SetLocalPosition2D(this Transform t, float x, float y) =>
        t.localPosition = new Vector3(x, y, t.localPosition.z);
}
#endif
