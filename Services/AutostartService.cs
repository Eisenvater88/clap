using Microsoft.Win32;

namespace Clap.Services;

/// <summary>Verwaltet den automatischen Start beim Windows-Login (HKCU Run-Key, keine Adminrechte nötig).</summary>
public static class AutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Clap";

    public static void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null) return;

        if (enabled && Environment.ProcessPath is { } exePath)
            key.SetValue(ValueName, $"\"{exePath}\"");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
