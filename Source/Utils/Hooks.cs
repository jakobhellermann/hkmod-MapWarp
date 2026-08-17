using System;
using System.Collections.Generic;
using System.Reflection;
using MonoMod.RuntimeDetour;

namespace MapWarp.Source;

internal static class Hooks {
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static |
                                     BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly List<Hook> installed = [];

    internal static void Add(Type type, string method, Delegate hook) =>
        Add(type.GetMethod(method, Any) ?? throw new MissingMethodException(type.FullName, method), hook);

    internal static void Add(MethodBase target, Delegate hook) => installed.Add(new Hook(target, hook));

    internal static IEnumerable<MethodInfo> Methods(Type type, Func<MethodInfo, bool> predicate) {
        foreach (var m in type.GetMethods(Any))
            if (predicate(m))
                yield return m;
    }

    internal static void UninstallAll() {
        foreach (var hook in installed)
            try {
                hook.Dispose();
            } catch (Exception e) {
                Logging.Error(e);
            }

        installed.Clear();
    }
}
