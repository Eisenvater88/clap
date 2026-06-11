namespace Clap.Models;

public sealed class AppSettings
{
    /// <summary>API-Key, per DPAPI (CurrentUser) verschlüsselt und Base64-kodiert.</summary>
    public string? ApiKeyProtected { get; set; }

    /// <summary>Claude-Modell-ID, z. B. "claude-opus-4-8".</summary>
    public string Model { get; set; } = "claude-opus-4-8";

    /// <summary>Standard-Zielsprache für Übersetzungen.</summary>
    public string TargetLanguage { get; set; } = "Deutsch";

    /// <summary>Bevorzugter globaler Shortcut (Name aus HotkeyService.Options).</summary>
    public string Hotkey { get; set; } = "Strg+Win+C";

    public bool Autostart { get; set; } = true;
}
