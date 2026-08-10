using System.Collections.Generic;
using UnityEngine;

namespace MapWarp.Source;

internal static class MapUtil {
    // OnGUI works in physical pixels, so fixed font sizes shrink on high-resolution screens. Scale everything
    // against the resolution the sizes were picked at.
    private const float ReferenceHeight = 1200f;

    internal static float GuiScale => Screen.height / ReferenceHeight;

    internal static GameObject[] Areas(GameMap map) => [
        map.areaAncientBasin, map.areaCity, map.areaCliffs, map.areaCrossroads, map.areaCrystalPeak,
        map.areaDeepnest, map.areaFogCanyon, map.areaFungalWastes, map.areaGreenpath, map.areaKingdomsEdge,
        map.areaQueensGardens, map.areaRestingGrounds, map.areaDirtmouth, map.areaWaterways
    ];

    // The GameMap object itself stays active while no map is shown; the game toggles the area objects
    // (WorldMap / QuickMap* activate them, CloseQuickMap deactivates all).
    internal static bool AnyAreaActive(GameMap map) {
        foreach (var area in Areas(map))
            if (area.activeInHierarchy)
                return true;
        return false;
    }

    // Map rooms are the direct children of the area objects, named after their scene (see GameMap.PositionCompass).
    internal static IEnumerable<(string name, SpriteRenderer sr)> Rooms(GameMap map, bool includeInactive) {
        foreach (var area in Areas(map)) {
            if (!includeInactive && !area.activeInHierarchy) continue;
            foreach (Transform room in area.transform) {
                if (!includeInactive && !room.gameObject.activeInHierarchy) continue;
                var sr = room.GetComponent<SpriteRenderer>();
                if (sr != null) yield return (room.name, sr);
            }
        }
    }

    // The on-screen rectangle the map render occupies. The game renders at cam.aspect into a texture shown
    // centered on screen with letterbox/pillarbox bars whenever the window aspect differs. Returned as
    // (dx, dy, dw, dh) in screen pixels, bottom-left origin. Reduces to the full screen when aspects match.
    private static (float dx, float dy, float dw, float dh) MapRect(Camera cam) {
        float sw = Screen.width, sh = Screen.height, a = cam.aspect;
        if (sw / sh > a) {
            var dw = sh * a;
            return ((sw - dw) * 0.5f, 0f, dw, sh);
        }

        var dh = sw / a;
        return (0f, (sh - dh) * 0.5f, sw, dh);
    }

    // Camera viewport point (0..1, bottom-left) to on-screen pixels (bottom-left origin), letterbox-corrected.
    internal static Vector2 ViewportToScreen(Camera cam, Vector3 vp) {
        var (dx, dy, dw, dh) = MapRect(cam);
        return new Vector2(dx + vp.x * dw, dy + vp.y * dh);
    }

    // World point to on-screen pixels (bottom-left), for hit-testing against Input.mousePosition.
    // cam.WorldToScreenPoint would return render-texture pixels — wrong under letterboxing / render scale.
    internal static Vector2 WorldToScreen(Camera cam, Vector3 world) =>
        ViewportToScreen(cam, cam.WorldToViewportPoint(world));

    // World point in GL.LoadOrtho space (0..1 across the whole screen), letterbox-corrected. GL.LoadOrtho maps
    // the full screen, while cam.WorldToViewportPoint is relative to the camera's own (narrower) rect.
    internal static Vector2 WorldToOrtho(Camera cam, Vector3 world) {
        var s = WorldToScreen(cam, world);
        return new Vector2(s.x / Screen.width, s.y / Screen.height);
    }

    // World point to OnGUI coordinates (top-left origin), letterbox-corrected.
    internal static Vector2 WorldToGui(Camera cam, Vector3 world) {
        var s = WorldToScreen(cam, world);
        return new Vector2(s.x, Screen.height - s.y);
    }
}
