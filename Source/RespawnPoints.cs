using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace MapWarp.Source;

// Safe respawn points per scene, normalized to [0,1] (world = normalized * sceneSize, as in MapTeleport).
// Generated offline for every room and embedded as `mapwarp_respawns.json` (see `tools/extract_respawns.sh`).
internal static class RespawnPoints {
    private const string ResourceName = "mapwarp_respawns.json";

    private static Dictionary<string, List<Vector2>> Data => field ??= LoadEmbedded();

    private static Dictionary<string, List<Vector2>> LoadEmbedded() {
        var asm = typeof(RespawnPoints).Assembly;
        var name = Array.Find(asm.GetManifestResourceNames(),
                       n => n.EndsWith(ResourceName, StringComparison.Ordinal))
                   ?? throw new FileNotFoundException($"embedded resource {ResourceName} missing");

        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        var parsed = Parse(reader.ReadToEnd());
        Logging.Info($"Respawn points: {parsed.Count} embedded scenes");
        return parsed;
    }

    private static Dictionary<string, List<Vector2>> Parse(string json) {
        var result = new Dictionary<string, List<Vector2>>();
        var raw = JsonConvert.DeserializeObject<Dictionary<string, List<float[]>>>(json)
                  ?? throw new InvalidDataException($"{ResourceName} did not parse into a scene map");
        foreach (var (scene, pts) in raw) {
            var list = new List<Vector2>(pts.Count);
            foreach (var p in pts)
                if (p.Length >= 2)
                    list.Add(new Vector2(p[0], p[1]));
            result[scene] = list;
        }

        return result;
    }

    internal static IReadOnlyList<Vector2>? Get(string scene) => Data.GetValueOrDefault(scene);
}
