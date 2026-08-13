using Modding;

namespace MapWarp.Source.Compat;

/// The v42 API of 1.2.2.1 names a mod after its type instead of taking a display name.
public abstract class ModBase : Mod {
#if !HK1221
    protected ModBase() : base("MapWarp") { }
#endif
}
