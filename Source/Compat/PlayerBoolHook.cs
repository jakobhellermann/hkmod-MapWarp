using System;
using System.Collections.Generic;
using Modding;

namespace MapWarp.Compat;

/// v42 passes the hook only the field name and treats a result differing from PlayerData's own value as an override.
internal static class PlayerBoolHook {
    private static readonly Dictionary<Func<string, bool, bool>, Delegate> proxies = new();

    internal static void Add(Func<string, bool, bool> hook) {
#if HK1221
        GetBoolProxy proxy = name => hook(name, PlayerData.instance.GetBoolInternal(name));
        ModHooks.Instance.GetPlayerBoolHook += proxy;
#else
        var proxy = new Modding.Delegates.GetBoolProxy(hook);
        ModHooks.GetPlayerBoolHook += proxy;
#endif
        proxies.Add(hook, proxy);
    }

    internal static void Remove(Func<string, bool, bool> hook) {
        var proxy = proxies[hook];
        proxies.Remove(hook);
#if HK1221
        ModHooks.Instance.GetPlayerBoolHook -= (GetBoolProxy)proxy;
#else
        ModHooks.GetPlayerBoolHook -= (Modding.Delegates.GetBoolProxy)proxy;
#endif
    }
}
