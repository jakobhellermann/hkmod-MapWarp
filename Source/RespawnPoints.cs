using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MapWarp;

// Safe respawn points per scene, normalized to the map rect.
// Generated offline (see `tools/extract_respawns.sh`).
internal static class RespawnPoints {
    private const string ResourceName = "mapwarp_respawns.bin";

    private sealed class Scene {
        internal Vector2 Offset;
        internal Vector2 Size;
        internal List<Vector2> Points = null!;
    }

    private static Dictionary<string, Scene> Data => field ??= LoadEmbedded();

    private static Dictionary<string, Scene> LoadEmbedded() {
        var asm = typeof(RespawnPoints).Assembly;
        var name = Array.Find(asm.GetManifestResourceNames(),
                       n => n.EndsWith(ResourceName, StringComparison.Ordinal))
                   ?? throw new FileNotFoundException($"embedded resource {ResourceName} missing");

        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new BinaryReader(stream);

        var sceneCount = reader.ReadInt32();
        var result = new Dictionary<string, Scene>(sceneCount);
        var points = 0;
        for (var s = 0; s < sceneCount; s++) {
            var scene = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32()));
            var entry = new Scene {
                Offset = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                Size = new Vector2(reader.ReadSingle(), reader.ReadSingle())
            };
            var pointCount = reader.ReadInt32();
            entry.Points = new List<Vector2>(pointCount);
            for (var p = 0; p < pointCount; p++)
                entry.Points.Add(new Vector2(reader.ReadSingle(), reader.ReadSingle()));
            result[scene] = entry;
            points += pointCount;
        }

        if (stream.Position != stream.Length)
            throw new InvalidOperationException(
                $"{ResourceName}: read {stream.Position} of {stream.Length} bytes after {sceneCount} scenes");

        return result;
    }

    internal static IList<Vector2>? Get(string scene) => Data.GetValueOrDefault(scene)?.Points;

    /// A point normalized within the scene's map rect, back to a world position in that scene.
    internal static Vector2 ToWorld(string scene, Vector2 normalized) {
        var entry = Data.GetValueOrDefault(scene)
                    ?? throw new KeyNotFoundException($"no map rect for scene {scene}");
        return new Vector2(normalized.x * entry.Size.x - entry.Offset.x,
            normalized.y * entry.Size.y - entry.Offset.y);
    }
}
