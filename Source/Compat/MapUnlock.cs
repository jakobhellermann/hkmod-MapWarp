using System;

namespace MapWarp.Compat;

/// 1.2.2.1's GameMap reads the PlayerData fields directly rather than through GetBool, so the read hook cannot
/// reach it. Writing them for the duration of a call is the only way; permanently would alter the save.
internal sealed class MapUnlock : IDisposable {
    private readonly string[] bools;
    private readonly bool[] saved;

    private MapUnlock(string[] bools, bool[] saved) {
        this.bools = bools;
        this.saved = saved;
    }

    internal static MapUnlock Begin(string[] bools) {
#if HK1221
        var pd = PlayerData.instance;
        var saved = new bool[bools.Length];
        for (var i = 0; i < bools.Length; i++) {
            saved[i] = pd.GetBoolInternal(bools[i]);
            pd.SetBoolInternal(bools[i], true);
        }

        return new MapUnlock(bools, saved);
#else
        return new MapUnlock([], []);
#endif
    }

    public void Dispose() {
        var pd = PlayerData.instance;
        for (var i = 0; i < bools.Length; i++)
            pd.SetBoolInternal(bools[i], saved[i]);
    }
}
