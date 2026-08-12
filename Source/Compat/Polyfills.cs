using System.Collections.Generic;
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
