using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Clap.Services;

/// <summary>
/// Registriert den systemweiten Hotkey über ein Message-Only-Window.
/// Ist der bevorzugte Shortcut belegt (z. B. reserviert Windows Strg+Win+C für die
/// Farbfilter-Funktion, auch wenn diese deaktiviert ist), wird automatisch auf
/// eine Alternative ausgewichen.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0xC1A9;

    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    public sealed record HotkeyOption(string Name, uint Modifiers, uint VirtualKey);

    /// <summary>Wählbare Shortcuts; Reihenfolge = Fallback-Reihenfolge.</summary>
    public static readonly HotkeyOption[] Options =
    [
        new("Strg+Win+C", ModControl | ModWin, 0x43),
        new("Strg+Alt+C", ModControl | ModAlt, 0x43),
        new("Strg+Win+Y", ModControl | ModWin, 0x59),
    ];

    private HwndSource? _source;
    private bool _registered;

    /// <summary>Name des tatsächlich registrierten Shortcuts, sonst null.</summary>
    public string? ActiveHotkeyName { get; private set; }

    public event Action? HotkeyPressed;

    /// <summary>Versucht zuerst den bevorzugten Shortcut, dann die Alternativen.</summary>
    public bool Register(string preferredName)
    {
        EnsureSource();
        Unregister();

        foreach (var option in Options.OrderBy(o => o.Name == preferredName ? 0 : 1))
        {
            if (RegisterHotKey(_source!.Handle, HotkeyId, option.Modifiers | ModNoRepeat, option.VirtualKey))
            {
                _registered = true;
                ActiveHotkeyName = option.Name;
                return true;
            }
        }

        ActiveHotkeyName = null;
        return false;
    }

    private void EnsureSource()
    {
        if (_source is not null) return;

        var parameters = new HwndSourceParameters("ClapHotkeyWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            // HWND_MESSAGE: reines Message-Window ohne UI
            ParentWindow = new IntPtr(-3),
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    private void Unregister()
    {
        if (_registered && _source is not null)
        {
            UnregisterHotKey(_source.Handle, HotkeyId);
            _registered = false;
            ActiveHotkeyName = null;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            HotkeyPressed?.Invoke();
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _source?.Dispose();
        _source = null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
