using UnityEngine;

namespace MapWarp;

internal static class MapRoomBorders {
    private const float LabelWidth = 220f;
    private const float LabelHeight = 22f;

    private static Material? mat;
    private static (string name, SpriteRenderer sr)[]? scenes;

    private static GUIStyle? labelStyle;
    private static float labelStyleScale;

    internal static void Rebuild() {
        if (!mat) mat = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };

        var gameMap = MapLifecycle.Current;
        if (gameMap == null) {
            scenes = null;
            return;
        }

        scenes = [..MapUtil.Rooms(gameMap, includeInactive: true)];
    }

    internal static void Cleanup() {
        scenes = null;
        if (mat) Object.Destroy(mat);
        mat = null;
    }

    internal static void DrawLabels(Camera cam) {
        if (!MapWarpPlugin.Settings.ShowRoomBorders) return;
        if (scenes == null) return;

        var scale = MapUtil.GuiScale;
        if (labelStyle == null || !Mathf.Approximately(labelStyleScale, scale)) {
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(12 * scale) };
            labelStyleScale = scale;
        }

        // Only label rooms when zoomed in at least to the default view; hide the clutter when zoomed out.
        var map = MapLifecycle.Current;
        if (map != null && map.transform.localScale.x < MapPanZoom.DefaultMapScale) return;

        foreach (var (name, sr) in scenes) {
            // Only rooms the map is actually showing - with "full map in quickmap" off the game leaves other
            // zones' rooms inactive, so this keeps the overlay in sync instead of boxing every room.
            if (!sr.gameObject.activeInHierarchy) continue;
            if (!OnScreen(cam, sr.bounds)) continue;
            var guiPos = MapUtil.WorldToGui(cam,
                new Vector3(sr.bounds.min.x, sr.bounds.max.y, sr.bounds.center.z));
            float w = LabelWidth * scale, h = LabelHeight * scale;
            var x = Mathf.Clamp(guiPos.x, 0f, Screen.width - w);
            var y = Mathf.Clamp(guiPos.y, 0f, Screen.height - h);
            GUI.Label(new Rect(x, y, w, h), name, labelStyle);
        }
    }

    internal static void DrawBorders(Camera cam) {
        if (!MapWarpPlugin.Settings.ShowRoomBorders) return;
        if (scenes == null || scenes.Length == 0 || mat == null) return;

        GL.PushMatrix();
        try {
            mat.SetPass(0);
            GL.LoadIdentity();
            GL.LoadOrtho();
            GL.Begin(GL.LINES);
            try {
                foreach (var (name, sr) in scenes) {
                    if (!sr.gameObject.activeInHierarchy) continue;
                    if (!OnScreen(cam, sr.bounds)) continue;
                    var min = MapUtil.WorldToOrtho(cam, sr.bounds.min);
                    var max = MapUtil.WorldToOrtho(cam, sr.bounds.max);
                    var hue = Mathf.Abs(name.GetHashCode()) % 1000 / 1000f;
                    GL.Color(Color.HSVToRGB(hue, 0.6f, 1f) with { a = 0.8f });
                    DrawRect(min.x, min.y, max.x, max.y);
                }
            } finally {
                GL.End();
            }
        } finally {
            GL.PopMatrix();
        }
    }

    private static bool OnScreen(Camera cam, Bounds b) {
        var min = MapUtil.WorldToOrtho(cam, b.min);
        var max = MapUtil.WorldToOrtho(cam, b.max);
        return max.x >= 0 && min.x <= 1 && max.y >= 0 && min.y <= 1;
    }

    private static void DrawRect(float x0, float y0, float x1, float y1) {
        GL.Vertex3(x0, y0, 0);
        GL.Vertex3(x1, y0, 0);
        GL.Vertex3(x1, y0, 0);
        GL.Vertex3(x1, y1, 0);
        GL.Vertex3(x1, y1, 0);
        GL.Vertex3(x0, y1, 0);
        GL.Vertex3(x0, y1, 0);
        GL.Vertex3(x0, y0, 0);
    }
}
