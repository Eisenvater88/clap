using System.Drawing;
using Forms = System.Windows.Forms;

namespace Clap.Services;

/// <summary>Tray-Icon mit Statusanzeige (aktiv/pausiert), Kontextmenü und Benachrichtigungen.</summary>
public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _pauseItem;
    private Icon? _activeIcon;
    private Icon? _pausedIcon;

    public event Action<bool>? PausedChanged;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    public bool IsPaused { get; private set; }

    private string _hotkeyText = "kein Shortcut aktiv";

    public TrayIconService()
    {
        _activeIcon = CreateIcon(Color.FromArgb(230, 126, 34));
        _pausedIcon = CreateIcon(Color.FromArgb(140, 140, 140));

        _pauseItem = new Forms.ToolStripMenuItem("Pausieren") { CheckOnClick = true };
        _pauseItem.CheckedChanged += (_, _) => SetPaused(_pauseItem.Checked);

        var settingsItem = new Forms.ToolStripMenuItem("Einstellungen…");
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke();

        var exitItem = new Forms.ToolStripMenuItem("Beenden");
        exitItem.Click += (_, _) => ExitRequested?.Invoke();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_pauseItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _activeIcon,
            Visible = true,
            ContextMenuStrip = menu,
        };
        _notifyIcon.DoubleClick += (_, _) => SettingsRequested?.Invoke();
        UpdateTooltip();
    }

    /// <summary>Zeigt den tatsächlich registrierten Shortcut im Tooltip an.</summary>
    public void SetHotkeyText(string? hotkey)
    {
        _hotkeyText = hotkey ?? "kein Shortcut aktiv";
        UpdateTooltip();
    }

    private void SetPaused(bool paused)
    {
        IsPaused = paused;
        _notifyIcon.Icon = paused ? _pausedIcon : _activeIcon;
        UpdateTooltip();
        PausedChanged?.Invoke(paused);
    }

    private void UpdateTooltip()
    {
        // NotifyIcon.Text ist auf 63 Zeichen begrenzt
        var text = IsPaused
            ? "Clap – KI-Assistent (pausiert)"
            : $"Clap – KI-Assistent — {_hotkeyText}";
        _notifyIcon.Text = text.Length <= 63 ? text : text[..63];
    }

    public void ShowNotification(string title, string message, bool isError = false)
    {
        _notifyIcon.ShowBalloonTip(4000, title, message,
            isError ? Forms.ToolTipIcon.Warning : Forms.ToolTipIcon.Info);
    }

    /// <summary>Einfaches programmatisch erzeugtes Icon (gefüllter Kreis mit „C“).</summary>
    private static Icon CreateIcon(Color color)
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(color);
            graphics.FillEllipse(brush, 1, 1, 30, 30);
            using var font = new Font("Segoe UI", 16, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
            var size = graphics.MeasureString("C", font);
            graphics.DrawString("C", font, Brushes.White, (32 - size.Width) / 2, (32 - size.Height) / 2);
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _activeIcon?.Dispose();
        _pausedIcon?.Dispose();
        _activeIcon = null;
        _pausedIcon = null;
    }
}
