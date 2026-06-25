namespace Clap.Models;

/// <summary>
/// Steuert, wie viel Markdown-Auszeichnung in der ausgegebenen KI-Antwort verbleibt.
/// Wird nachträglich auf den gestreamten Text angewendet – der Prompt bleibt unberührt.
/// </summary>
public enum OutputFormat
{
    /// <summary>Antwort wird unverändert ausgegeben; Markdown bleibt vollständig erhalten.</summary>
    Markdown,

    /// <summary>
    /// Reiner Klartext: alle Markdown-Auszeichnungen werden entfernt – inklusive der
    /// Aufzählungszeichen. Geeignet für Copy &amp; Paste in Programme ohne Markdown-Unterstützung.
    /// </summary>
    PlainText,

    /// <summary>
    /// Struktur (Aufzählungen, Absätze) bleibt erhalten; Fett, Kursiv, Überschriften und
    /// sonstige Auszeichnungen werden entfernt.
    /// </summary>
    SimpleStructure,
}
