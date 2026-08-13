using System;

namespace MapWarp.Source.Compat;

/// From 1.3.1.5 on the API routes PlayerData reads through GetBool, so PlayerBoolHook already reveals the map.
/// 1.2.2.1's GameMap reads the fields directly. They have to hold while it builds the map, including while
/// WorldMap widens the pan limits that GameMap.Update clamps the map position to.
internal sealed class MapUnlock : IDisposable {
    private readonly string[] bools;
    private readonly bool[] saved;

    private MapUnlock(string[] bools, bool[] saved) {
        this.bools = bools;
        this.saved = saved;
    }

    /// Null where intercepting the reads is enough.
    internal static MapUnlock? Begin(string[] bools) {
#if HK1221
        var pd = PlayerData.instance;
        var saved = new bool[bools.Length];
        for (var i = 0; i < bools.Length; i++) {
            saved[i] = pd.GetBoolInternal(bools[i]);
            pd.SetBoolInternal(bools[i], true);
        }

        return new MapUnlock(bools, saved);
#else
        return null;
#endif
    }

    public void Dispose() {
        var pd = PlayerData.instance;
        for (var i = 0; i < bools.Length; i++)
            pd.SetBoolInternal(bools[i], saved[i]);
    }
}
