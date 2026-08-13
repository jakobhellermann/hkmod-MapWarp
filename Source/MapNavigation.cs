using System;
using Modding;
using UnityEngine;

using MapWarp.Source.Compat;

namespace MapWarp.Source;

// Run before GameMap.Update so a pan moves the map before MapTeleport's hook draws the respawn-point markers.
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Camera))]
public class MapNavigation : MonoBehaviour {
    private const float ZoomSpeed = 0.15f;

    // Authored Game Map scales: 0.436 in the inventory's wide map (World Map FSM "Zoom Out"), 1.3 zoomed into
    // a scene map ("Zoomed In"), 1.55 in the quick map (Quick Map FSM "Check Area").
    private const float WideMapScale = 0.436f;
    internal const float DefaultMapScale = 1.5f;
    private const float MinScale = WideMapScale;
    private const float MaxScale = 8f;

    private Camera cam = null!;
    private bool dragging;
    private Vector3 dragOrigin;
    private Vector3 dragMapLocal;
    private GUIStyle? previewStyle;
    private float previewStyleScale;

    // True while a map is open. The InputHandler patch below reads this to keep the OS cursor visible and
    // unlocked on the map. Without it the game locks the cursor when idle (everywhere but menus), which both
    // warps the cursor to screen-center and makes Input.mousePosition read center.
    internal static bool MapOpen;

    private void Awake() {
        cam = GetComponent<Camera>();
    }

    private void Update() {
        try {
            var map = MapTeleport.Current;
            MapOpen = map != null && MapUtil.AnyAreaActive(map);
            if (!MapOpen) return;

            HandleDrag(map!);
            HandleZoom(map!);
        } catch (Exception e) {
            Logging.Error(e);
        }
    }

    private void OnDisable() => MapOpen = false;

    // Draw the teleport target (the room under the cursor, computed by MapTeleport) next to the cursor,
    // plus the room's known safe respawn points as markers on its map sprite.
    private void OnGUI() {
        try {
            if (!MapOpen) return;
            var room = MapTeleport.PreviewRoom;
            if (room is null or "") return;

            DrawRespawnPoints();

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
            previewStyle.normal.textColor = MapRoomBorders.AreaTint(room);

            var content = new GUIContent(room);
            var size = previewStyle.CalcSize(content);
            // Input.mousePosition (screen space, bottom-left origin) — absolute, so unlike
            // Event.current.mousePosition it isn't affected by GUI-matrix state between OnGUI passes.
            var mp = Input.mousePosition;
            // Place the label up-left of the cursor (offset by its own size) so the cursor never covers it.
            var rect = new Rect(mp.x - size.x, Screen.height - mp.y - size.y, size.x, size.y);

            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
            GUI.Label(rect, content, previewStyle);
        } catch (Exception e) {
            Logging.Error(e);
        }
    }

    // Mark the safe respawn points of every room currently under the cursor (MapTeleport.PreviewCandidates) — so
    // where room boxes overlap you see all of their spots, not just the selected room's. Points are stored
    // normalized [0,1] within a scene; each room's own map-sprite bounds map normalized -> world -> screen.
    private void DrawRespawnPoints() {
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

    private Vector3 MouseWorldPoint() {
        var mp = Input.mousePosition;
        var viewport = new Vector3(mp.x / Screen.width, mp.y / Screen.height, 0f);
        return cam.ViewportToWorldPoint(viewport);
    }

    private void HandleDrag(GameMap map) {
        if (Input.GetMouseButtonDown(0)) {
            dragOrigin = MouseWorldPoint();
            dragMapLocal = map.transform.localPosition;
            dragging = true;
        }

        if (!Input.GetMouseButton(0)) {
            dragging = false;
            return;
        }

        if (!dragging) return;
        var current = MouseWorldPoint();

        var localDelta = map.transform.parent.InverseTransformVector(current - dragOrigin);
        map.transform.SetLocalPosition2D(dragMapLocal.x + localDelta.x, dragMapLocal.y + localDelta.y);
    }

    private void HandleZoom(GameMap map) {
        var scroll = Input.mouseScrollDelta.y;
        if (scroll == 0) return;

        // Zoom by scaling the map (camera fixed), composing on the game's own zoom scale.
        var t = map.transform;
        var cursorWorld = MouseWorldPoint();
        var pivotLocal = t.InverseTransformPoint(cursorWorld);
        var s = Mathf.Clamp(t.localScale.x * (1f + scroll * ZoomSpeed), MinScale, MaxScale);
        t.localScale = new Vector3(s, s, t.localScale.z);
        // Reposition so the map point under the cursor stays put.
        var worldShift = cursorWorld - t.TransformPoint(pivotLocal);
        var localShift = t.parent.InverseTransformVector(worldShift);
        t.SetLocalPosition2D(t.localPosition.x + localShift.x, t.localPosition.y + localShift.y);
    }

    public static void Install() {
        foreach (var old in UnityCompat.FindAll<MapNavigation>(includeInactive: true))
            Destroy(old);
        var cam = GameCameras.instance != null ? GameCameras.instance.hudCamera : null;
        if (cam == null) return;
        cam.gameObject.AddComponent<MapNavigation>();
    }
}

// While a map is open, force the game's own cursor handling to keep the OS cursor enabled (visible +
// unlocked). InputHandler otherwise locks the cursor whenever there's no mouse movement (everywhere but
// menus), which warps it to screen-center and makes Input.mousePosition read center — breaking the mouse
// features and the hover preview whenever the cursor is held still.
//
// From 1.5.12620 on InputHandler.OnGUI sets thr cursor with
// SetCursorEnabled, while before that it delegates to ModHooks.OnCursor every frame. On 1432 it gets overwritten, on 1315 the method doesn't exist.
internal static class MapNavigationCursor {
#if HK1512620
    internal static void Install() {
        Hooks.Add(typeof(InputHandler), "SetCursorEnabled", (Action<Action<bool>, bool>)SetCursorEnabled);
    }

    internal static void Uninstall() { }

    private static void SetCursorEnabled(Action<bool> orig, bool isEnabled) {
        orig(MapNavigation.MapOpen || isEnabled);
    }
#else
    internal static void Install() => ModHooks.CursorHook += OnCursor;

    internal static void Uninstall() => ModHooks.CursorHook -= OnCursor;

    // A registered CursorHook replaces ModHooks.OnCursor's default entirely, so the else branch has to
    // reproduce it: visible only while paused.
    private static void OnCursor() {
        if (MapNavigation.MapOpen) {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        } else {
            Cursor.visible = GameManager.instance.isPaused;
        }
    }
#endif
}
