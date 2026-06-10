using System.Runtime.CompilerServices;
using Anthropic;
using Anthropic.Models.Messages;
using Clap.Models;

namespace Clap.Services;

public sealed class ClaudeService
{
    private readonly SettingsService _settingsService;
    private AnthropicClient? _client;
    private string? _clientApiKey;

    public ClaudeService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_settingsService.GetApiKey());

    private AnthropicClient GetClient()
    {
        var apiKey = _settingsService.GetApiKey()
            ?? throw new InvalidOperationException("Kein API-Key konfiguriert. Bitte in den Einstellungen hinterlegen.");
        if (_client is null || _clientApiKey != apiKey)
        {
            _client = new AnthropicClient { ApiKey = apiKey };
            _clientApiKey = apiKey;
        }
        return _client;
    }

    private Model GetModel() => _settingsService.Settings.Model switch
    {
        "claude-haiku-4-5" => Model.ClaudeHaiku4_5,
        "claude-sonnet-4-6" => Model.ClaudeSonnet4_6,
        _ => Model.ClaudeOpus4_8,
    };

    public async IAsyncEnumerable<string> StreamAsync(
        ClapAction action,
        CaptureResult capture,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var client = GetClient();
        var (system, content) = BuildRequest(action, capture);

        var parameters = new MessageCreateParams
        {
            Model = GetModel(),
            MaxTokens = 8192,
            System = system,
            Messages = [new() { Role = Role.User, Content = content }],
        };

        await foreach (var streamEvent in client.Messages.CreateStreaming(parameters).WithCancellation(ct))
        {
            if (streamEvent.TryPickContentBlockDelta(out var delta) &&
                delta.Delta.TryPickText(out var text))
            {
                yield return text.Text;
            }
        }
    }

    private static (string System, List<ContentBlockParam> Content) BuildRequest(ClapAction action, CaptureResult capture)
    {
        if (action.Kind == ClapActionKind.AnalyzeImage)
        {
            if (capture.ImagePngBase64 is null)
                throw new InvalidOperationException("Kein Bild in der Zwischenablage gefunden.");

            const string imageSystem =
                "Du bist ein Assistent zur Bildanalyse in einem Unternehmensumfeld. " +
                "Beschreibe und interpretiere Bildinhalte präzise und sachlich auf Deutsch. " +
                "Diagramme und Tabellen liest du inhaltlich korrekt aus und fasst ihre Kernaussagen zusammen.";

            return (imageSystem, new List<ContentBlockParam>
            {
                new ImageBlockParam
                {
                    Source = new Base64ImageSource
                    {
                        Data = capture.ImagePngBase64,
                        MediaType = MediaType.ImagePng,
                    },
                },
                new TextBlockParam
                {
                    Text = "Analysiere dieses Bild. Beschreibe den Inhalt, interpretiere Diagramme oder Tabellen " +
                           "inhaltlich korrekt und fasse die wesentlichen Aussagen zusammen.",
                },
            });
        }

        var text = capture.Text
            ?? throw new InvalidOperationException("Kein markierter Text gefunden.");

        var system = action.Kind switch
        {
            ClapActionKind.Translate =>
                "Du bist ein professioneller Übersetzer in einem Unternehmensumfeld. " +
                $"Erkenne die Sprache des Textes automatisch und übersetze ihn nach {action.Parameter}. " +
                "Übersetze kontextbezogen, berücksichtige Fachbegriffe und behalte Ton und Formatierung bei. " +
                "Gib ausschließlich die Übersetzung aus, ohne Erklärungen oder Vorbemerkungen.",

            ClapActionKind.Summarize =>
                "Fasse den folgenden Text kompakt zusammen. Die Zusammenfassung enthält die zentralen Aussagen " +
                "und Kernpunkte des Originals, ist sprachlich korrekt und verständlich formuliert. " +
                "Antworte auf Deutsch. Gib nur die Zusammenfassung aus, ohne Vorbemerkungen.",

            ClapActionKind.Explain =>
                "Erkläre den folgenden Inhalt in einfacher, allgemeinverständlicher Sprache auf Deutsch. " +
                "Löse Fachbegriffe auf oder versieh sie mit kurzen Erläuterungen. Bleibe inhaltlich korrekt. " +
                "Ergänze, wo es hilft, kurze Beispiele oder Analogien zur Veranschaulichung.",

            ClapActionKind.Rephrase =>
                $"Formuliere den folgenden Text um. Gewünschter Stil: {action.Parameter}. " +
                "Die inhaltliche Bedeutung muss vollständig erhalten bleiben. Grammatik und Rechtschreibung " +
                "müssen fehlerfrei sein. Behalte die Sprache des Originals bei. " +
                "Gib ausschließlich den umformulierten Text aus, ohne Erklärungen.",

            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

        return (system, new List<ContentBlockParam> { new TextBlockParam { Text = text } });
    }
}
