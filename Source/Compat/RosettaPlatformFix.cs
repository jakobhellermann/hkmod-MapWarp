using System;
using System.Runtime.InteropServices;

namespace MapWarp.Source.Compat;

/// MonoMod 21.x reads the OS and word size from Environment, which stay correct under translation, but adds
/// Platform.ARM from `uname -m`, spawned as a child process and therefore reporting the host's arm64. It then picks
/// the ARM detour backend, whose cache flush emits ARM instructions and dies with SIGILL on the first hook.
/// MonoMod 25 detects the translation itself.
internal static class RosettaPlatformFix {
    /// Must run before anything reads PlatformHelper.Current: the first read latches it and the setter then throws.
    internal static void Apply() {
#if !HK1512620
        if (!IsTranslated()) return;

        try {
            MonoMod.Utils.PlatformHelper.Current = Correct;
        } catch (InvalidOperationException) {
            // Already latched by an earlier reader, which is fine as long as it latched onto the right platform.
            if ((MonoMod.Utils.PlatformHelper.Current & (Correct | MonoMod.Utils.Platform.ARM)) != Correct)
                throw new InvalidOperationException(
                    $"MonoMod latched onto {MonoMod.Utils.PlatformHelper.Current} in a Rosetta-translated process; " +
                    "hooks would crash with SIGILL.");
        }
#endif
    }

#if !HK1512620
    // The game defines its own Platform in the global namespace from 1.4.3.2 on, which shadows an import.
    private const MonoMod.Utils.Platform Correct = MonoMod.Utils.Platform.MacOS | MonoMod.Utils.Platform.Bits64;

    private static bool IsTranslated() {
        var size = (IntPtr)sizeof(int);
        return sysctlbyname("sysctl.proc_translated", out var translated, ref size, IntPtr.Zero, IntPtr.Zero) == 0
               && translated == 1;
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int sysctlbyname(string name, out int oldp, ref IntPtr oldlenp, IntPtr newp, IntPtr newlen);
#endif
}
