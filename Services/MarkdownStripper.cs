using System.Text;
using System.Text.RegularExpressions;
using Clap.Models;

namespace Clap.Services;

/// <summary>
/// Entfernt Markdown-Auszeichnungen aus gestreamtem Text. Arbeitet zeilenweise und puffert
/// über Chunk-Grenzen hinweg, damit am Zeilenanfang stehende Konstrukte (Überschriften,
/// Aufzählungen, Zitate, Codeblöcke) zuverlässig erkannt werden, auch wenn ein Chunk
/// mitten in einer Zeile endet.
///
/// <para>
/// Bei <see cref="OutputFormat.PlainText"/> werden zusätzlich die Aufzählungszeichen entfernt,
/// bei <see cref="OutputFormat.SimpleStructure"/> bleiben Aufzählungen und Absätze erhalten.
/// Bei <see cref="OutputFormat.Markdown"/> wird der Text unverändert durchgereicht.
/// </para>
/// </summary>
public sealed partial class MarkdownStripper
{
    private readonly OutputFormat _format;
    private readonly StringBuilder _buffer = new();
    private bool _inFencedCode;

    public MarkdownStripper(OutputFormat format) => _format = format;

    /// <summary>True, wenn das gewählte Format überhaupt eine Bearbeitung vornimmt.</summary>
    public bool IsActive => _format != OutputFormat.Markdown;

    /// <summary>
    /// Verarbeitet ein Stück des Streams und gibt die bereits fertig formatierten,
    /// vollständigen Zeilen zurück. Ein angefangener Zeilenrest verbleibt im Puffer.
    /// </summary>
    public string Process(string chunk)
    {
        if (!IsActive) return chunk;

        _buffer.Append(chunk);
        var text = _buffer.ToString();

        var output = new StringBuilder();
        var consumed = 0;
        int newline;
        while ((newline = text.IndexOf('\n', consumed)) >= 0)
        {
            // Zeileninhalt ohne abschließendes '\n' (ein evtl. '\r' bleibt erhalten).
            var line = text[consumed..newline];
            output.Append(TransformLine(line));
            output.Append('\n');
            consumed = newline + 1;
        }

        _buffer.Clear();
        _buffer.Append(text, consumed, text.Length - consumed);

        return output.ToString();
    }

    /// <summary>Verarbeitet den am Stream-Ende verbliebenen Zeilenrest.</summary>
    public string Flush()
    {
        if (!IsActive || _buffer.Length == 0)
        {
            var rest = _buffer.ToString();
            _buffer.Clear();
            return rest;
        }

        var line = _buffer.ToString();
        _buffer.Clear();
        return TransformLine(line);
    }

    private string TransformLine(string line)
    {
        // Codeblock-Zäune (``` / ~~~) werden entfernt; der eingeschlossene Code bleibt
        // unangetastet, damit z. B. echte Sternchen oder Unterstriche im Code erhalten bleiben.
        if (FenceRegex().IsMatch(line))
        {
            _inFencedCode = !_inFencedCode;
            return "";
        }
        if (_inFencedCode) return line;

        var result = HeadingRegex().Replace(line, "");
        result = BlockquoteRegex().Replace(result, "");

        // Trennlinien (---, ***, ___) ganz entfernen.
        if (HorizontalRuleRegex().IsMatch(result)) return "";

        result = _format switch
        {
            // Aufzählungs-/Nummerierungszeichen entfernen, Einrückung beibehalten.
            OutputFormat.PlainText => ListMarkerRegex().Replace(result, "$1"),
            // Aufzählungszeichen auf einheitliches "- " normalisieren, Nummerierung belassen.
            OutputFormat.SimpleStructure => BulletMarkerRegex().Replace(result, "$1- "),
            _ => result,
        };

        return StripInline(result);
    }

    /// <summary>Entfernt Inline-Auszeichnungen (Links, Code, fett, kursiv, durchgestrichen).</summary>
    private static string StripInline(string s)
    {
        s = ImageRegex().Replace(s, "$1");
        s = LinkRegex().Replace(s, "$1");
        s = InlineCodeRegex().Replace(s, "$1");

        s = BoldItalicAsteriskRegex().Replace(s, "$1");
        s = BoldAsteriskRegex().Replace(s, "$1");
        s = ItalicAsteriskRegex().Replace(s, "$1");

        s = BoldItalicUnderscoreRegex().Replace(s, "$1");
        s = BoldUnderscoreRegex().Replace(s, "$1");
        s = ItalicUnderscoreRegex().Replace(s, "$1");

        s = StrikethroughRegex().Replace(s, "$1");
        return s;
    }

    // --- Zeilen-Konstrukte ---

    [GeneratedRegex(@"^\s*(?:```|~~~)")]
    private static partial Regex FenceRegex();

    [GeneratedRegex(@"^\s*#{1,6}\s*")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^(\s*)>\s?")]
    private static partial Regex BlockquoteRegex();

    [GeneratedRegex(@"^\s*([-*_])(?:\s*\1){2,}\s*\r?$")]
    private static partial Regex HorizontalRuleRegex();

    [GeneratedRegex(@"^(\s*)(?:[-*+]|\d+[.)])\s+")]
    private static partial Regex ListMarkerRegex();

    [GeneratedRegex(@"^(\s*)[-*+]\s+")]
    private static partial Regex BulletMarkerRegex();

    // --- Inline-Konstrukte ---
    // Emphasis-Marker dürfen in Markdown nicht direkt an Leerzeichen grenzen
    // (\S-Lookarounds) – das verhindert versehentliche Treffer wie "2 * 3 * 4".
    // Unterstrich-Varianten zusätzlich mit Wortgrenzen, damit snake_case unberührt bleibt.

    [GeneratedRegex(@"!\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex ImageRegex();

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"\*\*\*(?=\S)(.+?)(?<=\S)\*\*\*")]
    private static partial Regex BoldItalicAsteriskRegex();

    [GeneratedRegex(@"\*\*(?=\S)(.+?)(?<=\S)\*\*")]
    private static partial Regex BoldAsteriskRegex();

    [GeneratedRegex(@"\*(?=\S)([^*]+?)(?<=\S)\*")]
    private static partial Regex ItalicAsteriskRegex();

    [GeneratedRegex(@"(?<!\w)___(?=\S)(.+?)(?<=\S)___(?!\w)")]
    private static partial Regex BoldItalicUnderscoreRegex();

    [GeneratedRegex(@"(?<!\w)__(?=\S)(.+?)(?<=\S)__(?!\w)")]
    private static partial Regex BoldUnderscoreRegex();

    [GeneratedRegex(@"(?<!\w)_(?=\S)([^_]+?)(?<=\S)_(?!\w)")]
    private static partial Regex ItalicUnderscoreRegex();

    [GeneratedRegex(@"~~(?=\S)(.+?)(?<=\S)~~")]
    private static partial Regex StrikethroughRegex();
}
