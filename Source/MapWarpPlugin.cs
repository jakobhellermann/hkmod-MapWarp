using System.Reflection;
using JetBrains.Annotations;
using MapWarp.Source.Toasts;
using Modding;
using Object = UnityEngine.Object;

using MapWarp.Source.Compat;

namespace MapWarp.Source;

[UsedImplicitly]
public partial class MapWarpPlugin : ModBase, ITogglableMod {
    internal static Settings Settings = new();

    public override string GetVersion() => Assembly.GetExecutingAssembly().GetName().Version.ToString();

    public override void Initialize() {
        base.Initialize();

        Logging.Init(this);
        RosettaPlatformFix.Apply();

        MapLifecycle.Install();
        MapTeleport.Install();
        MapReveal.Install();
        CompassAlways.Install();
        MapNavigationCursor.Install();
        ToastManager.Install();

        // For hot-reloading
        MapLifecycle.Dispatch();
        
        Logging.Info($"Plugin {Name} has loaded!");
    }

    public void Unload() {
        // Clean up everything, in order to support hot reloading
        Hooks.UninstallAll();
        MapReveal.Uninstall();
        CompassAlways.Uninstall();

        MapRoomBorders.Cleanup();
        foreach (var c in UnityCompat.FindAll<UpdateDriver>(includeInactive: true))
            Object.Destroy(c);
        foreach (var c in UnityCompat.FindAll<ToastManager>(includeInactive: true))
            Object.Destroy(c.gameObject);

        Logging.Info($"Plugin {Name} has been unloaded!");
    }
}
