using Clap.Services;
using Xunit;

namespace Clap.Tests.Services;

/// <summary>
/// Tests für die statische Hotkey-Konfiguration und die Fallback-Reihenfolge.
/// Die eigentliche Win32-Registrierung (RegisterHotKey) ist nicht unit-testbar und
/// wird hier bewusst ausgeklammert.
/// </summary>
public sealed class HotkeyServiceTests
{
    [Fact]
    public void Options_ContainsThreeDistinctNamedShortcuts()
    {
        Assert.Equal(3, HotkeyService.Options.Length);

        var names = HotkeyService.Options.Select(o => o.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.Contains("Strg+Win+C", names);
        Assert.Contains("Strg+Alt+C", names);
        Assert.Contains("Strg+Win+Y", names);
    }

    [Fact]
    public void Options_DefaultPreferredShortcut_IsFirst()
    {
        // Reihenfolge der Liste = Fallback-Reihenfolge; der Standard steht vorn.
        Assert.Equal("Strg+Win+C", HotkeyService.Options[0].Name);
    }

    [Fact]
    public void PreferredShortcut_IsSortedToFront_LikeRegisterDoes()
    {
        // Repliziert die in Register() genutzte Sortierung und prüft die Fallback-Priorität:
        // der bevorzugte Shortcut wird zuerst probiert, die übrigen behalten ihre Reihenfolge.
        const string preferred = "Strg+Win+Y";
        var ordered = HotkeyService.Options
            .OrderBy(o => o.Name == preferred ? 0 : 1)
            .Select(o => o.Name)
            .ToList();

        Assert.Equal("Strg+Win+Y", ordered[0]);
        Assert.Equal(new[] { "Strg+Win+Y", "Strg+Win+C", "Strg+Alt+C" }, ordered);
    }

    [Fact]
    public void Options_AllHaveNonZeroModifiersAndKey()
    {
        Assert.All(HotkeyService.Options, o =>
        {
            Assert.False(string.IsNullOrWhiteSpace(o.Name));
            Assert.NotEqual(0u, o.Modifiers);
            Assert.NotEqual(0u, o.VirtualKey);
        });
    }
}
