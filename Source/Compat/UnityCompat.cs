using UnityEngine;
using Object = UnityEngine.Object;

namespace MapWarp.Source.Compat;

/// https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Object.FindObjectsByType.html
internal static class UnityCompat {
    internal static T[] FindAll<T>(bool includeInactive = false) where T : Object =>
#if HK1512620
        Object.FindObjectsByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
#else
        Object.FindObjectsOfType<T>(includeInactive);
#endif

    internal static T? FindFirst<T>() where T : Object =>
#if HK1512620
        Object.FindFirstObjectByType<T>();
#else
        Object.FindObjectOfType<T>();
#endif
}
