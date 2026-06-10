using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Clap.Services;

/// <summary>Registriert den systemweiten Hotkey Strg+Win+C über ein Message-Only-Window.</summary>
public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0xC1A9;

    private const uint ModControl = 0x0002;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkC = 0x43;

    private HwndSource? _source;
    private bool _registered;

    public event Action? HotkeyPressed;

    public bool Register()
    {
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

        _registered = RegisterHotKey(_source.Handle, HotkeyId, ModControl | ModWin | ModNoRepeat, VkC);
        return _registered;
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
        if (_source is not null)
        {
            if (_registered)
                UnregisterHotKey(_source.Handle, HotkeyId);
            _source.Dispose();
            _source = null;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
