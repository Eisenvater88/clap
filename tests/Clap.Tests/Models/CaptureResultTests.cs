using Clap.Models;
using Xunit;

namespace Clap.Tests.Models;

/// <summary>Tests für die abgeleiteten Eigenschaften der Erfassung (HasText/HasImage/IsEmpty).</summary>
public sealed class CaptureResultTests
{
    [Fact]
    public void WithText_HasTextAndIsNotEmpty()
    {
        var result = new CaptureResult { Text = "Hallo" };
        Assert.True(result.HasText);
        Assert.False(result.HasImage);
        Assert.False(result.IsEmpty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void WhitespaceOrEmptyText_CountsAsNoText(string text)
    {
        var result = new CaptureResult { Text = text };
        Assert.False(result.HasText);
    }

    [Fact]
    public void WithImage_HasImageAndIsNotEmpty()
    {
        var result = new CaptureResult { ImagePngBase64 = "iVBORw0KGgo=" };
        Assert.True(result.HasImage);
        Assert.False(result.HasText);
        Assert.False(result.IsEmpty);
    }

    [Fact]
    public void Nothing_IsEmpty()
    {
        var result = new CaptureResult();
        Assert.True(result.IsEmpty);
        Assert.False(result.HasText);
        Assert.False(result.HasImage);
    }

    [Fact]
    public void TextAndImage_BothReported()
    {
        var result = new CaptureResult { Text = "Hallo", ImagePngBase64 = "abc" };
        Assert.True(result.HasText);
        Assert.True(result.HasImage);
        Assert.False(result.IsEmpty);
    }
}
