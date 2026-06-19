using System.IO;
using System.Text.Json;
using Clap.Models;

namespace Clap.Services;

public sealed class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clap");
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    public AppSettings Settings { get; internal set; } = new();

    public event Action? SettingsChanged;

    public static SettingsService Load()
    {
        var service = new SettingsService();
        try
        {
            if (File.Exists(SettingsPath))
            {
                service.Settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new();
            }
        }
        catch
        {
            // Defekte Datei → mit Defaults starten
            service.Settings = new();
        }

        ApplyMigrations(service.Settings);
        return service;
    }

    /// <summary>
    /// Passt geladene Einstellungen an aktuelle Annahmen an. Aktuell: ein alter
    /// Anthropic-Modellname ("claude…") ist für Ollama ungültig → zurücksetzen, damit
    /// beim ersten Start ein lokales Modell ausgewählt wird.
    /// </summary>
    internal static void ApplyMigrations(AppSettings settings)
    {
        if (settings.Model.StartsWith("claude", StringComparison.OrdinalIgnoreCase))
            settings.Model = "";
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDir);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true }));
        SettingsChanged?.Invoke();
    }
}
