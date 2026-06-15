namespace Clap.Models;

public sealed class AppSettings
{
    /// <summary>Basis-URL des Ollama-Servers (lokal oder im Netz erreichbar).</summary>
    public string OllamaUrl { get; set; } = "http://localhost:11434";

    /// <summary>Ollama-Textmodell für Übersetzen/Zusammenfassen/Erklären/Umformulieren, z. B. "qwen3:4b".</summary>
    public string Model { get; set; } = "";

    /// <summary>Optionales Vision-Modell für die Bildanalyse, z. B. "llava". Leer = Bildanalyse deaktiviert.</summary>
    public string VisionModel { get; set; } = "";

    /// <summary>
    /// Kontextfenster (Tokens). Begrenzt den Speicherbedarf — das riesige Standard-Fenster
    /// mancher Modelle (z. B. qwen3) sprengt sonst den Arbeitsspeicher. Höher = mehr Text,
    /// aber mehr RAM. 8192 ist ein sicherer Standard für ~16 GB-Maschinen.
    /// </summary>
    public int NumCtx { get; set; } = 8192;

    /// <summary>Standard-Zielsprache für Übersetzungen.</summary>
    public string TargetLanguage { get; set; } = "Deutsch";

    /// <summary>
    /// Persönlicher Stil-Leitfaden (Markdown) für die Umformulieren-Aktionen: bevorzugte
    /// Wortwahl, häufig genutzte Formulierungen, zu vermeidende Wörter. Wird ausschließlich
    /// beim Umformulieren in den System-Prompt eingebunden – nicht beim Übersetzen,
    /// Zusammenfassen, Erklären oder bei der Bildanalyse. Leer = generischer Stil.
    /// </summary>
    public string RephraseStyleGuide { get; set; } = "";

    /// <summary>Bevorzugter globaler Shortcut (Name aus HotkeyService.Options).</summary>
    public string Hotkey { get; set; } = "Strg+Win+C";

    public bool Autostart { get; set; } = true;
}
