namespace Clap.Models;

public enum ClapActionKind
{
    Translate,
    Summarize,
    Explain,
    Rephrase,
    Proofread,
    AnalyzeImage,
}

/// <param name="Parameter">Zielsprache (Translate) bzw. Stil (Rephrase), sonst null.</param>
public sealed record ClapAction(ClapActionKind Kind, string? Parameter = null)
{
    public string DisplayName => Kind switch
    {
        ClapActionKind.Translate => $"Übersetzen → {Parameter}",
        ClapActionKind.Summarize => "Zusammenfassen",
        ClapActionKind.Explain => "Erklären",
        ClapActionKind.Rephrase => $"Umformulieren ({Parameter})",
        ClapActionKind.Proofread => "Rechtschreibung & Grammatik korrigieren",
        ClapActionKind.AnalyzeImage => "Bild analysieren",
        _ => Kind.ToString(),
    };
}
