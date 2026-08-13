using System;
using System.Collections.Generic;
using System.Reflection;
using MapWarp.Source.Toasts;
using Modding;
using UnityEngine;
using Object = UnityEngine.Object;

using MapWarp.Source.Compat;

namespace MapWarp.Source;

public partial class MapWarpPlugin : ModBase, ITogglableMod {
    internal static Settings Settings = new();

    public override string GetVersion() => Assembly.GetExecutingAssembly().GetName().Version.ToString();

    public override void Initialize() {
        base.Initialize();

        Logging.Init(this);
        RosettaPlatformFix.Apply();
        Logging.Info($"Plugin {Name} has loaded!");

        MapLifecycle.Install();
        MapTeleport.Install();
        MapReveal.Install();
        CompassAlways.Install();
        MapNavigationCursor.Install();
        ToastManager.Install();

        // Hot reload: the GameMap may already exist when the plugin (re)loads, so MapLifecycle's Start/
        // OnEnable hooks won't fire. Dispatch directly (each handler is a no-op when no map is present).
        MapLifecycle.Dispatch();
    }

    public void Unload() {
        // Clean up everything, in order to support hot reloading
        Hooks.UninstallAll();
        MapReveal.Uninstall();
        CompassAlways.Uninstall();

        foreach (var c in UnityCompat.FindAll<MapRoomBorders>(includeInactive: true))
            Object.Destroy(c);
        foreach (var c in UnityCompat.FindAll<MapNavigation>(includeInactive: true))
            Object.Destroy(c);
        foreach (var c in UnityCompat.FindAll<ToastManager>(includeInactive: true))
            Object.Destroy(c.gameObject);

        Logging.Info($"Plugin {Name} has been unloaded!");
    }
}
