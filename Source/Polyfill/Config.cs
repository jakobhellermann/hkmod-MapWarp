using System;

namespace MapWarp.Source.Polyfill;

// Polyfill for BepInEx Config
internal sealed class ConfigEntry<T>(T value) {
    internal T Value { get; set; } = value;

    internal event EventHandler? SettingChanged; // TODO
}

internal static class Config {
    internal static ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description) =>
        new(defaultValue);
}
