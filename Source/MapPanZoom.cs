using System;
using UnityEngine;

#if !HK1512620
using MapWarp.Compat;
#endif

namespace MapWarp;

internal static class MapPanZoom {
    private const float ZoomSpeed = 0.15f;

    private const float WideMapScale = 0.436f; // World Map FSM "Zoom Out"
    internal const float DefaultMapScale = 1.5f;
    private const float MinScale = WideMapScale;
    private const float MaxScale = 8f;

    private static bool dragging;
    private static Vector3 dragOrigin;
    private static Vector3 dragMapLocal;
    private static GUIStyle? previewStyle;
    private static float previewStyleScale;

    internal static void HandleFrame(GameMap map, Camera cam) {
        HandleDrag(map, cam);
        HandleZoom(map, cam);
    }

    internal static void DrawPreview(Camera cam) {
        var room = MapTeleport.PreviewRoom;
        if (room is null or "") return;

        DrawRespawnPoints(cam);

        var scale = MapUtil.GuiScale;
        if (previewStyle == null || !Mathf.Approximately(previewStyleScale, scale)) {
            var pad = Mathf.RoundToInt(5 * scale);
            previewStyle = new GUIStyle(GUI.skin.label) {
                fontSize = Mathf.RoundToInt(13 * scale), fontStyle = FontStyle.Bold,
                padding = new RectOffset(pad, pad, Mathf.RoundToInt(3 * scale), Mathf.RoundToInt(3 * scale)),
                richText = true
            };
            previewStyleScale = scale;
        }
        // Tint the label with the room's own area colour (its map sprite tint).
        previewStyle.normal.textColor = MapUtil.AreaTint(MapLifecycle.Current, room);

        var content = new GUIContent(room);
        var size = previewStyle.CalcSize(content);
        // Input.mousePosition (screen space, bottom-left origin) - absolute, so unlike
        // Event.current.mousePosition it isn't affected by GUI-matrix state between OnGUI passes.
        var mp = Input.mousePosition;
        // Place the label up-left of the cursor (offset by its own size) so the cursor never covers it.
        var rect = new Rect(mp.x - size.x, Screen.height - mp.y - size.y, size.x, size.y);

        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = prev;
        GUI.Label(rect, content, previewStyle);
    }

    // Mark the safe respawn points of every room currently under the cursor - so where room boxes overlap
    // you see all of their spots, not just the selected room's. Points are stored
    // normalized [0,1] within a scene; each room's own map-sprite bounds map normalized -> world -> screen.
    private static void DrawRespawnPoints(Camera cam) {
        if (!MapWarpPlugin.Settings.ShowRespawnPoints) return;

        var s = 12f * MapUtil.GuiScale;
        var prev = GUI.color;
        GUI.color = Color.white;
        foreach (var (room, b) in MapTeleport.PreviewCandidates) {
            if (b.size.x <= 0f || b.size.y <= 0f) continue;
            var points = RespawnPoints.Get(room);
            if (points == null) continue;
            foreach (var p in points) {
                var world = new Vector3(b.min.x + p.x * b.size.x, b.min.y + p.y * b.size.y, 0f);
                var g = MapUtil.WorldToGui(cam, world);
                GUI.DrawTexture(new Rect(g.x - s / 2f, g.y - s / 2f, s, s), DiamondTexture);
            }
        }

        GUI.color = prev;
    }

    // Diamond marker (dark border + teal fill) baked into one texture. Border width is measured as the
    // perpendicular distance to the fill edge, so it stays uniform along all four sides (a scaled second
    // diamond would bulge at the tips). Built once.
    private static Texture2D DiamondTexture => field ??= BuildDiamond(24, borderPx: 3f);

    private static Texture2D BuildDiamond(int size, float borderPx) {
        var fill = new Color(0.55f, 0.82f, 0.78f, 0.95f);
        var border = new Color(0f, 0f, 0f, 0.8f);
        const float sqrt2 = 1.4142136f;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var c = (size - 1) / 2f;
        var fillR = c - borderPx * sqrt2; // Manhattan radius of the fill; tips reach the texture edge at c.
        var px = new Color[size * size];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++) {
            // Perpendicular pixels past the fill edge (negative = inside fill).
            var dp = (Mathf.Abs(x - c) + Mathf.Abs(y - c) - fillR) / sqrt2;
            var fillCov = Mathf.Clamp01(0.5f - dp);          // 1 inside fill, AA across ~1px
            var outerCov = Mathf.Clamp01(0.5f - (dp - borderPx)); // fill + border silhouette
            var rgb = Color.Lerp(border, fill, fillCov);
            px[y * size + x] = new Color(rgb.r, rgb.g, rgb.b, outerCov * Mathf.Lerp(border.a, fill.a, fillCov));
        }

        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private static Vector3 MouseWorldPoint(Camera cam) {
        var mp = Input.mousePosition;
        var viewport = new Vector3(mp.x / Screen.width, mp.y / Screen.height, 0f);
        return cam.ViewportToWorldPoint(viewport);
    }

    private static void HandleDrag(GameMap map, Camera cam) {
        if (Input.GetMouseButtonDown(0)) {
            dragOrigin = MouseWorldPoint(cam);
            dragMapLocal = map.transform.localPosition;
            dragging = true;
        }

        if (!Input.GetMouseButton(0)) {
            dragging = false;
            return;
        }

        if (!dragging) return;
        var current = MouseWorldPoint(cam);

        var localDelta = map.transform.parent.InverseTransformVector(current - dragOrigin);
        map.transform.SetLocalPosition2D(dragMapLocal.x + localDelta.x, dragMapLocal.y + localDelta.y);
    }

    private static void HandleZoom(GameMap map, Camera cam) {
        var scroll = Input.mouseScrollDelta.y;
        if (scroll == 0) return;

        // Zoom by scaling the map (camera fixed), composing on the game's own zoom scale.
        var t = map.transform;
        var cursorWorld = MouseWorldPoint(cam);
        var pivotLocal = t.InverseTransformPoint(cursorWorld);
        var s = Mathf.Clamp(t.localScale.x * (1f + scroll * ZoomSpeed), MinScale, MaxScale);
        t.localScale = new Vector3(s, s, t.localScale.z);
        // Reposition so the map point under the cursor stays put.
        var worldShift = cursorWorld - t.TransformPoint(pivotLocal);
        var localShift = t.parent.InverseTransformVector(worldShift);
        t.SetLocalPosition2D(t.localPosition.x + localShift.x, t.localPosition.y + localShift.y);
    }
}

// While a map is open, keep the OS cursor visible and unlocked. InputHandler.OnGUI decides the cursor every frame
// and nothing else; it hides the cursor during gameplay, and on 1.2.2.1 also locks it, which warps the pointer to
// screen-center the moment it is assigned and makes Input.mousePosition read center.
internal static class MapNavigationCursor {
    internal static void Install() =>
        Hooks.Add(typeof(InputHandler), "OnGUI", (Action<Action<InputHandler>, InputHandler>)OnGUI);

    private static void OnGUI(Action<InputHandler> orig, InputHandler self) {
        if (!MapLifecycle.MapOpen) {
            orig(self);
            return;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
