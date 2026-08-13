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
}
