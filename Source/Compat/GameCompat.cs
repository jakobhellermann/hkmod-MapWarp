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
}

#if !HK1512620
internal static class TransformCompat {
    /// The game ships this from 1.5.12620 on: Extensions.SetLocalPosition2D
    internal static void SetLocalPosition2D(this Transform t, float x, float y) =>
        t.localPosition = new Vector3(x, y, t.localPosition.z);
}
#endif
