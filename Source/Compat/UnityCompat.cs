using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MapWarp.Source.Compat;

/// https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Object.FindObjectsByType.html
internal static class UnityCompat {
    internal static T[] FindAll<T>(bool includeInactive = false) where T : Object =>
#if HK1512620
        Object.FindObjectsByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
#elif HK1578
        Object.FindObjectsOfType<T>(includeInactive);
#else
        // FindObjectsOfType got its includeInactive overload in Unity 2020
        includeInactive ? Resources.FindObjectsOfTypeAll<T>() : Object.FindObjectsOfType<T>();
#endif

    internal static T? FindFirst<T>() where T : Object =>
#if HK1512620
        Object.FindFirstObjectByType<T>();
#else
        Object.FindObjectOfType<T>();
#endif

#if HK1221
    // Unity 5.4 has no SceneUtility to map build indices back to scene names, so ask per name. Cached because
    // this runs for every room on every frame a map is open.
    private static readonly Dictionary<string, bool> loadable = new();

    internal static bool IsLoadableScene(string sceneName) {
        if (loadable.TryGetValue(sceneName, out var known)) return known;

        var result = Application.CanStreamedLevelBeLoaded(sceneName);
        loadable[sceneName] = result;
        return result;
    }
#else
    internal static bool IsLoadableScene(string sceneName) => BuildScenes.Contains(sceneName);

    private static HashSet<string> BuildScenes => field ??= CollectBuildScenes();

    private static HashSet<string> CollectBuildScenes() {
        var scenes = new HashSet<string>();
        for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++)
            scenes.Add(Path.GetFileNameWithoutExtension(UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i)));
        return scenes;
    }
#endif

    /// Rigidbody2D.bodyType arrived in Unity 5.5.
    internal static void MakeDynamic(this Rigidbody2D body) {
#if HK1221
        body.isKinematic = false;
#else
        body.bodyType = RigidbodyType2D.Dynamic;
#endif
    }
}
