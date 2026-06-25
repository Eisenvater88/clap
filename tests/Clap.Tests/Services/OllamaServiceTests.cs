using Clap.Models;
using Clap.Services;
using Xunit;

namespace Clap.Tests.Services;

/// <summary>
/// Tests für OllamaService – ohne Netzwerk. Geprüft wird die reine Anfragelogik:
/// Modellauswahl, System-Prompts je Aktion, Stil-Leitfaden, Bild-/Fehlerbehandlung.
/// </summary>
public sealed class OllamaServiceTests
{
    private static OllamaService Create(AppSettings settings) =>
        new(new SettingsService { Settings = settings });

    private static CaptureResult Text(string text) => new() { Text = text };

    // --- Konfigurationsstatus -------------------------------------------------

    [Fact]
    public void IsConfigured_FalseWhenNoModel()
    {
        Assert.False(Create(new AppSettings { Model = "" }).IsConfigured);
        Assert.False(Create(new AppSettings { Model = "   " }).IsConfigured);
    }

    [Fact]
    public void IsConfigured_TrueWhenModelSet()
    {
        Assert.True(Create(new AppSettings { Model = "qwen3:4b" }).IsConfigured);
    }

    [Fact]
    public void HasVisionModel_ReflectsSetting()
    {
        Assert.False(Create(new AppSettings { VisionModel = "" }).HasVisionModel);
        Assert.True(Create(new AppSettings { VisionModel = "llava" }).HasVisionModel);
    }

    // --- BuildRequest: Modell & Nutzdaten ------------------------------------

    [Fact]
    public void BuildRequest_TextAction_UsesConfiguredTextModel()
    {
        var svc = Create(new AppSettings { Model = "qwen3:4b" });
        var (model, _, userText, image) = svc.BuildRequest(
            new ClapAction(ClapActionKind.Summarize), Text("Langer Text"));

        Assert.Equal("qwen3:4b", model);
        Assert.Equal("Langer Text", userText);
        Assert.Null(image);
    }

    // --- BuildRequest: System-Prompts je Aktion ------------------------------

    [Fact]
    public void BuildRequest_Translate_TargetsRequestedLanguage()
    {
        var svc = Create(new AppSettings { Model = "m" });
        var (_, system, _, _) = svc.BuildRequest(
            new ClapAction(ClapActionKind.Translate, "Englisch"), Text("Hallo"));

        Assert.Contains("übersetze ihn nach Englisch", system);
        Assert.Contains("Übersetzer", system);
    }

    [Fact]
    public void BuildRequest_Proofread_InstructsNotToRephrase()
    {
        var svc = Create(new AppSettings { Model = "m" });
        var (_, system, _, _) = svc.BuildRequest(
            new ClapAction(ClapActionKind.Proofread), Text("Text"));

        Assert.Contains("Rechtschreibung", system);
        Assert.Contains("NICHT um", system);
    }

    [Fact]
    public void BuildRequest_Explain_AsksForSimpleLanguage()
    {
        var svc = Create(new AppSettings { Model = "m" });
        var (_, system, _, _) = svc.BuildRequest(new ClapAction(ClapActionKind.Explain), Text("Text"));
        Assert.Contains("allgemeinverständlicher Sprache", system);
    }

    // --- BuildRequest: Stil-Leitfaden (nur beim Umformulieren) ---------------

    [Fact]
    public void BuildRequest_Rephrase_IncludesStyleAndStyleGuide()
    {
        var svc = Create(new AppSettings { Model = "m", RephraseStyleGuide = "Kurze Sätze. Kein Passiv." });
        var (_, system, _, _) = svc.BuildRequest(
            new ClapAction(ClapActionKind.Rephrase, "formell"), Text("Text"));

        Assert.Contains("Gewünschter Stil: formell", system);
        Assert.Contains("Kurze Sätze. Kein Passiv.", system);
        Assert.Contains("persönlichen Stilvorgaben", system);
    }

    [Fact]
    public void BuildRequest_Rephrase_WithoutStyleGuide_OmitsGuideSection()
    {
        var svc = Create(new AppSettings { Model = "m", RephraseStyleGuide = "" });
        var (_, system, _, _) = svc.BuildRequest(
            new ClapAction(ClapActionKind.Rephrase, "formell"), Text("Text"));

        Assert.Contains("Gewünschter Stil: formell", system);
        Assert.DoesNotContain("persönlichen Stilvorgaben", system);
    }

    [Fact]
    public void BuildRequest_StyleGuide_NotAppliedToTranslate()
    {
        // Der Stil-Leitfaden ist bewusst auf das Umformulieren beschränkt.
        var svc = Create(new AppSettings { Model = "m", RephraseStyleGuide = "MEIN-STIL-MARKER" });
        var (_, system, _, _) = svc.BuildRequest(
            new ClapAction(ClapActionKind.Translate, "Englisch"), Text("Text"));

        Assert.DoesNotContain("MEIN-STIL-MARKER", system);
    }

    // --- BuildRequest: Bildanalyse -------------------------------------------

    [Fact]
    public void BuildRequest_AnalyzeImage_UsesVisionModelAndImage()
    {
        var svc = Create(new AppSettings { VisionModel = "llava" });
        var capture = new CaptureResult { ImagePngBase64 = "BASE64DATA" };
        var (model, system, _, image) = svc.BuildRequest(new ClapAction(ClapActionKind.AnalyzeImage), capture);

        Assert.Equal("llava", model);
        Assert.Equal("BASE64DATA", image);
        Assert.Contains("Bildanalyse", system);
    }

    [Fact]
    public void BuildRequest_AnalyzeImage_WithoutVisionModel_Throws()
    {
        var svc = Create(new AppSettings { VisionModel = "" });
        var capture = new CaptureResult { ImagePngBase64 = "BASE64DATA" };

        var ex = Assert.Throws<InvalidOperationException>(
            () => svc.BuildRequest(new ClapAction(ClapActionKind.AnalyzeImage), capture));
        Assert.Contains("Vision-Modell", ex.Message);
    }

    [Fact]
    public void BuildRequest_AnalyzeImage_WithoutImage_Throws()
    {
        var svc = Create(new AppSettings { VisionModel = "llava" });

        var ex = Assert.Throws<InvalidOperationException>(
            () => svc.BuildRequest(new ClapAction(ClapActionKind.AnalyzeImage), new CaptureResult()));
        Assert.Contains("Kein Bild", ex.Message);
    }

    // --- BuildRequest: Fehlerfälle Text --------------------------------------

    [Fact]
    public void BuildRequest_TextAction_WithoutModel_Throws()
    {
        var svc = Create(new AppSettings { Model = "" });

        var ex = Assert.Throws<InvalidOperationException>(
            () => svc.BuildRequest(new ClapAction(ClapActionKind.Summarize), Text("Text")));
        Assert.Contains("Kein Textmodell", ex.Message);
    }

    [Fact]
    public void BuildRequest_TextAction_WithoutText_Throws()
    {
        var svc = Create(new AppSettings { Model = "m" });

        var ex = Assert.Throws<InvalidOperationException>(
            () => svc.BuildRequest(new ClapAction(ClapActionKind.Summarize), new CaptureResult()));
        Assert.Contains("Kein markierter Text", ex.Message);
    }

    // --- ExtractError ---------------------------------------------------------

    [Fact]
    public void ExtractError_ParsesJsonErrorField()
    {
        Assert.Equal("model not found", OllamaService.ExtractError("""{ "error": "model not found" }"""));
    }

    [Fact]
    public void ExtractError_NonJson_ReturnsRawBody()
    {
        Assert.Equal("Internal Server Error", OllamaService.ExtractError("Internal Server Error"));
    }

    [Fact]
    public void ExtractError_EmptyBody_ReturnsFallback()
    {
        Assert.Equal("Unbekannter Fehler.", OllamaService.ExtractError(""));
    }
}
