using System.Windows;
using Clap.Models;
using Clap.Services;
using Clap.Windows;

namespace Clap;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;
    private SettingsService _settingsService = null!;
    private ClaudeService _claudeService = null!;
    private TrayIconService? _trayIcon;
    private HotkeyService? _hotkeyService;
    private SettingsWindow? _settingsWindow;
    private bool _captureInProgress;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, "Clap_SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);

        _settingsService = SettingsService.Load();
        _claudeService = new ClaudeService(_settingsService);

        _trayIcon = new TrayIconService();
        _trayIcon.SettingsRequested += ShowSettings;
        _trayIcon.ExitRequested += () => Shutdown();

        _hotkeyService = new HotkeyService();
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        if (!_hotkeyService.Register())
        {
            _trayIcon.ShowNotification("Clap",
                "Der Shortcut Strg+Win+C konnte nicht registriert werden – er wird " +
                "möglicherweise von einer anderen Anwendung verwendet.", isError: true);
        }

        if (!_claudeService.IsConfigured)
        {
            ShowSettings();
        }
        else
        {
            _trayIcon.ShowNotification("Clap ist bereit",
                "Text markieren und Strg+Win+C drücken, um den KI-Assistenten zu öffnen.");
        }

        // Nach dem Start ungenutzten Speicher freigeben (Hintergrund-Betrieb)
        _ = Dispatcher.InvokeAsync(MemoryTrimmer.Trim,
            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private async void OnHotkeyPressed()
    {
        if (_trayIcon is null || _trayIcon.IsPaused || _captureInProgress) return;

        if (!_claudeService.IsConfigured)
        {
            ShowSettings();
            return;
        }

        _captureInProgress = true;
        try
        {
            var capture = await TextCaptureService.CaptureAsync();
            if (capture.IsEmpty)
            {
                _trayIcon.ShowNotification("Clap",
                    "Kein markierter Text und kein Bild in der Zwischenablage gefunden.");
                return;
            }

            var menu = new ActionMenuWindow(capture, _settingsService.Settings.TargetLanguage);
            menu.ActionSelected += action => ShowResult(action, capture);
            menu.Show();
        }
        catch (Exception ex)
        {
            _trayIcon.ShowNotification("Clap – Fehler", ex.Message, isError: true);
        }
        finally
        {
            _captureInProgress = false;
        }
    }

    private void ShowResult(ClapAction action, CaptureResult capture)
    {
        var window = new ResultWindow(_claudeService, action, capture);
        window.Show();
        window.Activate();
    }

    private void ShowSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow(_settingsService);
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        _trayIcon?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
