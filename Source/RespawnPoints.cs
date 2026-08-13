using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MapWarp.Source;

// Safe respawn points per scene, normalized to [0,1] (world = normalized * sceneSize, as in MapTeleport).
// Generated offline for every room and embedded as `mapwarp_respawns.bin` (see `tools/extract_respawns.sh`).
internal static class RespawnPoints {
    private const string ResourceName = "mapwarp_respawns.bin";

    private static Dictionary<string, List<Vector2>> Data => field ??= LoadEmbedded();

    private static Dictionary<string, List<Vector2>> LoadEmbedded() {
        var asm = typeof(RespawnPoints).Assembly;
        var name = Array.Find(asm.GetManifestResourceNames(),
                       n => n.EndsWith(ResourceName, StringComparison.Ordinal))
                   ?? throw new FileNotFoundException($"embedded resource {ResourceName} missing");

        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new BinaryReader(stream);

        var sceneCount = reader.ReadInt32();
        var result = new Dictionary<string, List<Vector2>>(sceneCount);
        var points = 0;
        for (var s = 0; s < sceneCount; s++) {
            var scene = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32()));
            var pointCount = reader.ReadInt32();
            var list = new List<Vector2>(pointCount);
            for (var p = 0; p < pointCount; p++) list.Add(new Vector2(reader.ReadSingle(), reader.ReadSingle()));
            result[scene] = list;
            points += pointCount;
        }

        // A truncated or overlong resource would otherwise surface as silently missing rooms.
        if (stream.Position != stream.Length)
            throw new InvalidOperationException(
                $"{ResourceName}: read {stream.Position} of {stream.Length} bytes after {sceneCount} scenes");

        Logging.Info($"Respawn points: {result.Count} embedded scenes, {points} points");
        return result;
    }

    internal static IList<Vector2>? Get(string scene) => Data.GetValueOrDefault(scene);
}
