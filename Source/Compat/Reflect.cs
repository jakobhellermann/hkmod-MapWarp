using System.Reflection;

namespace MapWarp.Source.Compat;

/// The Modding API only grew ReflectionHelper after 1.2.2.1.
internal static class Reflect {
    internal static TField GetField<TObject, TField>(TObject obj, string name) =>
        (TField)typeof(TObject)
            .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(obj);
}
