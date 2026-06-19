using Clap.Models;
using Xunit;

namespace Clap.Tests.Models;

/// <summary>Tests für die Anzeigenamen der Aktionen (im Menü sichtbar).</summary>
public sealed class ClapActionTests
{
    [Fact]
    public void Translate_IncludesTargetLanguage()
    {
        var action = new ClapAction(ClapActionKind.Translate, "Englisch");
        Assert.Equal("Übersetzen → Englisch", action.DisplayName);
    }

    [Fact]
    public void Rephrase_IncludesStyleInParentheses()
    {
        var action = new ClapAction(ClapActionKind.Rephrase, "formell");
        Assert.Equal("Umformulieren (formell)", action.DisplayName);
    }

    [Theory]
    [InlineData(ClapActionKind.Summarize, "Zusammenfassen")]
    [InlineData(ClapActionKind.Explain, "Erklären")]
    [InlineData(ClapActionKind.Proofread, "Rechtschreibung & Grammatik korrigieren")]
    [InlineData(ClapActionKind.AnalyzeImage, "Bild analysieren")]
    public void ParameterlessActions_HaveFixedNames(ClapActionKind kind, string expected)
    {
        Assert.Equal(expected, new ClapAction(kind).DisplayName);
    }

    [Fact]
    public void Record_SupportsValueEquality()
    {
        Assert.Equal(
            new ClapAction(ClapActionKind.Translate, "Deutsch"),
            new ClapAction(ClapActionKind.Translate, "Deutsch"));
        Assert.NotEqual(
            new ClapAction(ClapActionKind.Translate, "Deutsch"),
            new ClapAction(ClapActionKind.Translate, "Englisch"));
    }
}
