using System.Text.Json;
using Clap.Models;
using Xunit;

namespace Clap.Tests.Models;

/// <summary>Tests für Standardwerte und JSON-Persistenz der Einstellungen.</summary>
public sealed class AppSettingsTests
{
    [Fact]
    public void Defaults_AreSensible()
    {
        var settings = new AppSettings();

        Assert.Equal("http://localhost:11434", settings.OllamaUrl);
        Assert.Equal("", settings.Model);
        Assert.Equal("", settings.VisionModel);
        Assert.Equal(8192, settings.NumCtx);
        Assert.Equal("Deutsch", settings.TargetLanguage);
        Assert.Equal("", settings.RephraseStyleGuide);
        Assert.Equal("Strg+Win+C", settings.Hotkey);
        Assert.True(settings.Autostart);
    }

    [Fact]
    public void JsonRoundTrip_PreservesAllValues()
    {
        var original = new AppSettings
        {
            OllamaUrl = "http://server:11434",
            Model = "qwen3:4b",
            VisionModel = "llava",
            NumCtx = 4096,
            TargetLanguage = "Englisch",
            RephraseStyleGuide = "Kurze Sätze. Kein Passiv.",
            Hotkey = "Strg+Alt+C",
            Autostart = false,
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<AppSettings>(json)!;

        Assert.Equal(original.OllamaUrl, restored.OllamaUrl);
        Assert.Equal(original.Model, restored.Model);
        Assert.Equal(original.VisionModel, restored.VisionModel);
        Assert.Equal(original.NumCtx, restored.NumCtx);
        Assert.Equal(original.TargetLanguage, restored.TargetLanguage);
        Assert.Equal(original.RephraseStyleGuide, restored.RephraseStyleGuide);
        Assert.Equal(original.Hotkey, restored.Hotkey);
        Assert.Equal(original.Autostart, restored.Autostart);
    }

    [Fact]
    public void Deserialize_PartialJson_UsesDefaultsForMissingFields()
    {
        // Ältere/teilweise Konfigurationsdateien dürfen nicht zum Verlust von Defaults führen.
        var restored = JsonSerializer.Deserialize<AppSettings>("""{ "Model": "qwen3:4b" }""")!;

        Assert.Equal("qwen3:4b", restored.Model);
        Assert.Equal("http://localhost:11434", restored.OllamaUrl);
        Assert.Equal(8192, restored.NumCtx);
        Assert.True(restored.Autostart);
    }
}
