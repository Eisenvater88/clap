using System.Runtime.InteropServices;

namespace Clap.Services;

/// <summary>
/// Gibt nach Abschluss einer Interaktion ungenutzte Speicherseiten an das System zurück,
/// damit die App im Hintergrund ressourcenschonend bleibt.
/// </summary>
public static class MemoryTrimmer
{
    public static void Trim()
    {
        GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, -1, -1);
    }

    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, nint minSize, nint maxSize);
}
