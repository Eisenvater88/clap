namespace Clap.Models;

/// <summary>Ergebnis der Bildschirm-Erfassung nach dem Hotkey.</summary>
public sealed class CaptureResult
{
    /// <summary>Markierter Text, falls vorhanden.</summary>
    public string? Text { get; init; }

    /// <summary>Bild aus der Zwischenablage als PNG (Base64), falls vorhanden.</summary>
    public string? ImagePngBase64 { get; init; }

    public bool HasText => !string.IsNullOrWhiteSpace(Text);
    public bool HasImage => ImagePngBase64 is not null;
    public bool IsEmpty => !HasText && !HasImage;
}
