using Clap.Services;
using Xunit;

namespace Clap.Tests.Services;

/// <summary>
/// Tests für OllamaService.ThinkFilter: entfernt &lt;think&gt;…&lt;/think&gt;-Blöcke aus
/// dem gestreamten Text – auch dann korrekt, wenn ein Tag über eine Chunk-Grenze zerfällt.
/// Diese Logik ist das Herzstück der Stream-Aufbereitung und entsprechend gründlich getestet.
/// </summary>
public sealed class ThinkFilterTests
{
    /// <summary>Verarbeitet alle Chunks und hängt den Flush-Rest an – simuliert einen kompletten Stream.</summary>
    private static string Run(params string[] chunks)
    {
        var filter = new OllamaService.ThinkFilter();
        var output = "";
        foreach (var chunk in chunks)
            output += filter.Process(chunk);
        output += filter.Flush();
        return output;
    }

    [Fact]
    public void PlainText_PassesThrough()
    {
        Assert.Equal("Hallo Welt", Run("Hallo Welt"));
    }

    [Fact]
    public void EmptyInput_ProducesEmptyOutput()
    {
        Assert.Equal("", Run(""));
    }

    [Fact]
    public void RemovesCompleteThinkBlock()
    {
        Assert.Equal("Antwort", Run("<think>geheim</think>Antwort"));
    }

    [Fact]
    public void KeepsTextBeforeAndAfterThinkBlock()
    {
        Assert.Equal("AC", Run("A<think>B</think>C"));
    }

    [Fact]
    public void RemovesMultipleThinkBlocks()
    {
        Assert.Equal("XYZ", Run("X<think>1</think>Y<think>2</think>Z"));
    }

    [Fact]
    public void ThinkBlockSplitAcrossChunks_IsRemoved()
    {
        // Der öffnende Tag <think> ist auf zwei Chunks verteilt.
        Assert.Equal("HalloWelt", Run("Hallo<thi", "nk>Gedanke</think>Welt"));
    }

    [Fact]
    public void ClosingTagSplitAcrossChunks_IsRemoved()
    {
        Assert.Equal("Ergebnis", Run("<think>denken</thi", "nk>Ergebnis"));
    }

    [Fact]
    public void OpenTagOneCharPerChunk_IsRemoved()
    {
        // Jedes Zeichen einzeln gestreamt – der robusteste Grenzfall.
        var chunks = new[] { "<", "t", "h", "i", "n", "k", ">", "x", "<", "/", "t", "h", "i", "n", "k", ">", "!" };
        Assert.Equal("!", Run(chunks));
    }

    [Fact]
    public void UnclosedThinkBlock_SuppressesTrailingThoughts()
    {
        // Stream endet mitten im Denken → nichts vom Gedanken darf durchsickern.
        Assert.Equal("sichtbar", Run("sichtbar<think>noch am Denken..."));
    }

    [Fact]
    public void LiteralLessThan_NotAThinkTag_IsKept()
    {
        Assert.Equal("a < b und c < d", Run("a < b und c < d"));
    }

    [Fact]
    public void TrailingPartialTagPrefix_IsNotLost()
    {
        // Endet auf "<" (möglicher Tag-Anfang) – wird zurückgehalten, aber per Flush ausgegeben.
        Assert.Equal("Preis a<", Run("Preis a<"));
    }

    [Fact]
    public void ContentAfterDoneStyleStreaming_Accumulates()
    {
        // Mehrere Inhalts-Chunks ohne Think werden einfach konkateniert.
        Assert.Equal("Dies ist eine Antwort.", Run("Dies ", "ist ", "eine ", "Antwort."));
    }
}
