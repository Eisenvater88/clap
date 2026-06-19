using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Clap.Models;

namespace Clap.Services;

/// <summary>
/// Spricht einen lokalen (oder im Netz erreichbaren) Ollama-Server über die
/// native /api/chat-Schnittstelle an. Antworten werden als NDJSON gestreamt.
/// </summary>
public sealed class OllamaService
{
    private readonly SettingsService _settingsService;

    // Kein Gesamt-Timeout: Streaming-Antworten können beliebig lange laufen.
    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    public OllamaService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>Ein Textmodell ist konfiguriert.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settingsService.Settings.Model);

    /// <summary>Ein Vision-Modell ist hinterlegt → Bildanalyse verfügbar.</summary>
    public bool HasVisionModel => !string.IsNullOrWhiteSpace(_settingsService.Settings.VisionModel);

    private string BaseUrl => _settingsService.Settings.OllamaUrl.TrimEnd('/');

    /// <summary>Liste der auf dem Server installierten Modelle (für die Einstellungen).</summary>
    public async Task<IReadOnlyList<string>> GetModelsAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(4));
            var tags = await Http.GetFromJsonAsync<TagsResponse>($"{BaseUrl}/api/tags", cts.Token);
            return tags?.Models?.Select(m => m.Name).Where(n => !string.IsNullOrEmpty(n)).ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async IAsyncEnumerable<string> StreamAsync(
        ClapAction action,
        CaptureResult capture,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (model, system, userText, imageBase64) = BuildRequest(action, capture);

        using var response = await OpenChatStreamAsync(model, system, userText, imageBase64, ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var thinkFilter = new ThinkFilter();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (line.Length == 0) continue;

            ChatChunk? chunk;
            try { chunk = JsonSerializer.Deserialize<ChatChunk>(line); }
            catch (JsonException) { continue; }

            if (chunk?.Message?.Content is { Length: > 0 } content)
            {
                var visible = thinkFilter.Process(content);
                if (visible.Length > 0) yield return visible;
            }

            if (chunk?.Done == true) break;
        }

        var tail = thinkFilter.Flush();
        if (tail.Length > 0) yield return tail;
    }

    /// <summary>
    /// Sendet die Chat-Anfrage. Bei „Thinking"-Modellen (z. B. qwen3) wird per
    /// <c>think:true</c> erreicht, dass der Denkprozess in ein separates Feld wandert und
    /// <c>message.content</c> nur die eigentliche Antwort enthält. Modelle ohne
    /// Thinking-Unterstützung (z. B. Vision-Modelle) antworten mit 400 – dann wird ohne
    /// den Parameter erneut versucht.
    /// </summary>
    private async Task<HttpResponseMessage> OpenChatStreamAsync(
        string model, string system, string userText, string? imageBase64, CancellationToken ct)
    {
        var numCtx = _settingsService.Settings.NumCtx;

        for (var includeThink = true; ; includeThink = false)
        {
            HttpResponseMessage response;
            try
            {
                var payload = BuildPayload(model, system, userText, imageBase64, includeThink, numCtx);
                var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/chat")
                {
                    Content = JsonContent.Create(payload),
                };
                response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException(
                    $"Ollama unter {BaseUrl} ist nicht erreichbar. Läuft der Ollama-Dienst? ({ex.Message})");
            }

            if (response.IsSuccessStatusCode) return response;

            var body = await response.Content.ReadAsStringAsync(ct);
            var status = (int)response.StatusCode;
            response.Dispose();

            // Modell kennt kein „thinking" → ohne den Parameter wiederholen
            if (includeThink && status == 400 && body.Contains("think", StringComparison.OrdinalIgnoreCase))
                continue;

            throw new InvalidOperationException($"Ollama-Fehler ({status}): {ExtractError(body)}");
        }
    }

    private static Dictionary<string, object?> BuildPayload(
        string model, string system, string userText, string? imageBase64, bool includeThink, int numCtx)
    {
        var userMessage = new Dictionary<string, object?>
        {
            ["role"] = "user",
            ["content"] = userText,
        };
        if (imageBase64 is not null)
            userMessage["images"] = new[] { imageBase64 };

        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["messages"] = new object[]
            {
                new Dictionary<string, object?> { ["role"] = "system", ["content"] = system },
                userMessage,
            },
            ["stream"] = true,
            ["options"] = new Dictionary<string, object?>
            {
                ["temperature"] = 0.3,
                ["num_ctx"] = numCtx,
            },
        };
        if (includeThink)
            payload["think"] = true;

        return payload;
    }

    internal static string ExtractError(string body)
    {
        try
        {
            var doc = JsonSerializer.Deserialize<ErrorResponse>(body);
            if (!string.IsNullOrWhiteSpace(doc?.Error)) return doc!.Error!;
        }
        catch (JsonException) { }
        return string.IsNullOrWhiteSpace(body) ? "Unbekannter Fehler." : body;
    }

    internal (string Model, string System, string UserText, string? ImageBase64) BuildRequest(
        ClapAction action, CaptureResult capture)
    {
        var settings = _settingsService.Settings;

        if (action.Kind == ClapActionKind.AnalyzeImage)
        {
            if (capture.ImagePngBase64 is null)
                throw new InvalidOperationException("Kein Bild in der Zwischenablage gefunden.");
            if (string.IsNullOrWhiteSpace(settings.VisionModel))
                throw new InvalidOperationException(
                    "Für die Bildanalyse ist kein Vision-Modell konfiguriert. " +
                    "Bitte in den Einstellungen ein Modell wie \"llava\" oder \"qwen2.5vl\" hinterlegen " +
                    "(zuvor per \"ollama pull llava\" laden).");

            const string imageSystem =
                "Du bist ein Assistent zur Bildanalyse in einem Unternehmensumfeld. " +
                "Beschreibe und interpretiere Bildinhalte präzise und sachlich auf Deutsch. " +
                "Diagramme und Tabellen liest du inhaltlich korrekt aus und fasst ihre Kernaussagen zusammen.";

            const string imagePrompt =
                "Analysiere dieses Bild. Beschreibe den Inhalt, interpretiere Diagramme oder Tabellen " +
                "inhaltlich korrekt und fasse die wesentlichen Aussagen zusammen.";

            return (settings.VisionModel, imageSystem, imagePrompt, capture.ImagePngBase64);
        }

        if (string.IsNullOrWhiteSpace(settings.Model))
            throw new InvalidOperationException(
                "Kein Textmodell konfiguriert. Bitte in den Einstellungen ein Ollama-Modell wählen.");

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

            ClapActionKind.Proofread =>
                "Korrigiere im folgenden Text ausschließlich Rechtschreibung, Grammatik und Zeichensetzung. " +
                "Formuliere NICHT um: Wortwahl, Satzbau, Stil, Ton und Bedeutung bleiben unverändert. " +
                "Ändere nur, was eindeutig fehlerhaft ist; ist der Text bereits korrekt, gib ihn unverändert zurück. " +
                "Behalte die Sprache und Formatierung des Originals bei. " +
                "Gib ausschließlich den korrigierten Text aus, ohne Erklärungen oder Anmerkungen.",

            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

        // Persönlicher Stil-Leitfaden: bewusst NUR beim Umformulieren angehängt,
        // damit Übersetzungen & Co. neutral bleiben.
        if (action.Kind == ClapActionKind.Rephrase
            && !string.IsNullOrWhiteSpace(settings.RephraseStyleGuide))
        {
            system +=
                "\n\nBerücksichtige zusätzlich die folgenden persönlichen Stilvorgaben des Nutzers " +
                "(bevorzugte Wortwahl, häufig genutzte Formulierungen, zu vermeidende Wörter). " +
                "Wende sie an, damit der Text persönlicher klingt, solange Bedeutung, Grammatik und " +
                "Rechtschreibung korrekt bleiben:\n\n" +
                settings.RephraseStyleGuide.Trim();
        }

        return (settings.Model, system, text, null);
    }

    #region JSON-Modelle

    private sealed class ChatChunk
    {
        [JsonPropertyName("message")] public ChatMessage? Message { get; set; }
        [JsonPropertyName("done")] public bool Done { get; set; }
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("content")] public string? Content { get; set; }
    }

    private sealed class TagsResponse
    {
        [JsonPropertyName("models")] public List<TagModel>? Models { get; set; }
    }

    private sealed class TagModel
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
    }

    private sealed class ErrorResponse
    {
        [JsonPropertyName("error")] public string? Error { get; set; }
    }

    #endregion

    /// <summary>
    /// Entfernt <c>&lt;think&gt;…&lt;/think&gt;</c>-Blöcke aus dem gestreamten Text.
    /// Moderne Ollama-Versionen trennen „thinking" bereits ab; dieser Filter ist eine
    /// robuste Absicherung und arbeitet auch über Chunk-Grenzen hinweg korrekt.
    /// </summary>
    internal sealed class ThinkFilter
    {
        private const string Open = "<think>";
        private const string Close = "</think>";

        private bool _inThink;
        private string _buffer = "";

        public string Process(string chunk)
        {
            _buffer += chunk;
            var output = new StringBuilder();

            while (_buffer.Length > 0)
            {
                if (!_inThink)
                {
                    var open = _buffer.IndexOf(Open, StringComparison.Ordinal);
                    if (open < 0)
                    {
                        var safe = SafeEmitLength(_buffer, Open);
                        output.Append(_buffer, 0, safe);
                        _buffer = _buffer[safe..];
                        break;
                    }
                    output.Append(_buffer, 0, open);
                    _buffer = _buffer[(open + Open.Length)..];
                    _inThink = true;
                }
                else
                {
                    var close = _buffer.IndexOf(Close, StringComparison.Ordinal);
                    if (close < 0)
                    {
                        var safe = SafeEmitLength(_buffer, Close);
                        _buffer = _buffer[safe..];
                        break;
                    }
                    _buffer = _buffer[(close + Close.Length)..];
                    _inThink = false;
                }
            }

            return output.ToString();
        }

        /// <summary>Gibt am Stream-Ende verbliebenen Text außerhalb eines Think-Blocks zurück.</summary>
        public string Flush()
        {
            if (_inThink) return "";
            var rest = _buffer;
            _buffer = "";
            return rest;
        }

        /// <summary>
        /// Länge, die sicher ausgegeben werden kann, ohne ein über die Chunk-Grenze
        /// reichendes (Teil-)Tag zu zerschneiden. Hält ein mögliches Tag-Präfix zurück.
        /// </summary>
        private static int SafeEmitLength(string buffer, string tag)
        {
            var max = Math.Min(tag.Length - 1, buffer.Length);
            for (var k = max; k > 0; k--)
            {
                if (buffer.AsSpan(buffer.Length - k).SequenceEqual(tag.AsSpan(0, k)))
                    return buffer.Length - k;
            }
            return buffer.Length;
        }
    }
}
