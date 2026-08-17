
// ReSharper disable CheckNamespace

#if !NETSTANDARD2_1
namespace System.Collections.Generic {
    // https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.collectionextensions.getvalueordefault
    internal static class CollectionExtensionsPolyfill {
        public static TValue? GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key)
            where TKey : notnull
            => dictionary.TryGetValue(key, out var value) ? value : default;
    }

    // https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.keyvaluepair-2.deconstruct
    internal static class KeyValuePairPolyfill {
        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value) {
            key = pair.Key;
            value = pair.Value;
        }
    }
}
#endif

#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices {
    // https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.isexternalinit
    internal static class IsExternalInit { }
}
#endif

// From 1.3.1.5 on the Modding API bakes these into Assembly-CSharp. Public like the BCL types, so tuples stay
// usable in non-private signatures.
#if HK1221
namespace System {
    // https://learn.microsoft.com/en-us/dotnet/api/system.valuetuple-2
    public struct ValueTuple<T1, T2>(T1 item1, T2 item2) {
        public T1 Item1 = item1;
        public T2 Item2 = item2;
    }

    // https://learn.microsoft.com/en-us/dotnet/api/system.valuetuple-3
    public struct ValueTuple<T1, T2, T3>(T1 item1, T2 item2, T3 item3) {
        public T1 Item1 = item1;
        public T2 Item2 = item2;
        public T3 Item3 = item3;
    }

    // https://learn.microsoft.com/en-us/dotnet/api/system.valuetuple-4
    public struct ValueTuple<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4) {
        public T1 Item1 = item1;
        public T2 Item2 = item2;
        public T3 Item3 = item3;
        public T4 Item4 = item4;
    }
}

namespace System.Runtime.CompilerServices {
    // https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.tupleelementnamesattribute
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property |
                    AttributeTargets.ReturnValue | AttributeTargets.Class | AttributeTargets.Struct |
                    AttributeTargets.Event)]
    public sealed class TupleElementNamesAttribute(string[] transformNames) : Attribute {
        public string[] TransformNames { get; } = transformNames;
    }
}

#endif

// TryGetComponent arrived in Unity 2019.2; 1.5.78 (2020.2) and newer ship it.
#if HK1221 || HK1315 || HK1432
namespace UnityEngine {
    // https://docs.unity3d.com/ScriptReference/Component.TryGetComponent.html
    internal static class ComponentPolyfill {
        public static bool TryGetComponent<T>(this Component self, out T component) where T : Component {
            component = self.GetComponent<T>();
            return component != null;
        }
    }
}
#endif
