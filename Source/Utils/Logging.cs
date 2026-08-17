using JetBrains.Annotations;
using Modding;

namespace MapWarp;

[PublicAPI]
internal static class Logging {
    private static ILogger? logSource;

    internal static void Init(ILogger logSource) {
        Logging.logSource = logSource;
    }

    internal static void Debug(object data) {
        logSource?.LogDebug(data);
    }

    internal static void Error(object data) {
        logSource?.LogError(data);
    }

    internal static void Fatal(object data) {
        logSource?.LogError(data);
    }

    internal static void Info(object data) {
        logSource?.Log(data);
    }

    internal static void Message(object data) {
        logSource?.Log(data);
    }

    internal static void Warning(object data) {
        logSource?.LogWarn(data);
    }
}
