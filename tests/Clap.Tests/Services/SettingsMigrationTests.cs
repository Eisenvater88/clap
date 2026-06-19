using Clap.Models;
using Clap.Services;
using Xunit;

namespace Clap.Tests.Services;

/// <summary>
/// Tests für SettingsService.ApplyMigrations: ein alter Anthropic-Modellname ("claude…")
/// ist für Ollama ungültig und muss beim Laden zurückgesetzt werden.
/// </summary>
public sealed class SettingsMigrationTests
{
    [Theory]
    [InlineData("claude-3-5-sonnet-20241022")]
    [InlineData("claude-opus-4")]
    [InlineData("CLAUDE-3-haiku")] // Groß-/Kleinschreibung egal
    public void ClaudeModel_IsReset(string model)
    {
        var settings = new AppSettings { Model = model };
        SettingsService.ApplyMigrations(settings);
        Assert.Equal("", settings.Model);
    }

    [Theory]
    [InlineData("qwen3:4b")]
    [InlineData("llama3.1")]
    [InlineData("")]
    public void OllamaOrEmptyModel_IsLeftUnchanged(string model)
    {
        var settings = new AppSettings { Model = model };
        SettingsService.ApplyMigrations(settings);
        Assert.Equal(model, settings.Model);
    }

    [Fact]
    public void Migration_DoesNotTouchOtherFields()
    {
        var settings = new AppSettings { Model = "claude-x", VisionModel = "llava", TargetLanguage = "Englisch" };
        SettingsService.ApplyMigrations(settings);

        Assert.Equal("llava", settings.VisionModel);
        Assert.Equal("Englisch", settings.TargetLanguage);
    }
}
