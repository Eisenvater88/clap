using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Clap.Models;

namespace Clap.Services;

public sealed class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clap");
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    public AppSettings Settings { get; private set; } = new();

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
        return service;
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDir);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true }));
        SettingsChanged?.Invoke();
    }

    public string? GetApiKey()
    {
        if (string.IsNullOrEmpty(Settings.ApiKeyProtected)) return null;
        try
        {
            var protectedBytes = Convert.FromBase64String(Settings.ApiKeyProtected);
            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }

    public void SetApiKey(string apiKey)
    {
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(apiKey), null, DataProtectionScope.CurrentUser);
        Settings.ApiKeyProtected = Convert.ToBase64String(bytes);
    }
}
