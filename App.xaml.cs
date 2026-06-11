using System.Windows;
using Clap.Models;
using Clap.Services;
using Clap.Windows;

namespace Clap;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;
    private bool _ownsMutex;
    private SettingsService _settingsService = null!;
    private OllamaService _aiService = null!;
    private TrayIconService? _trayIcon;
    private HotkeyService? _hotkeyService;
    private SettingsWindow? _settingsWindow;
    private bool _captureInProgress;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, "Clap_SingleInstance", out var isFirstInstance);
        _ownsMutex = isFirstInstance;
        if (!isFirstInstance)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // Globale Auffanglinie: lieber eine Meldung als Absturz im Tray-Betrieb.
        DispatcherUnhandledException += (_, args) =>
        {
            _trayIcon?.ShowNotification("Clap – unerwarteter Fehler",
                args.Exception.Message, isError: true);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                _trayIcon?.ShowNotification("Clap – unerwarteter Fehler",
                    ex.Message, isError: true);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            args.SetObserved();
        };

        _settingsService = SettingsService.Load();
        _aiService = new OllamaService(_settingsService);

        _trayIcon = new TrayIconService();
        _trayIcon.SettingsRequested += ShowSettings;
        _trayIcon.ExitRequested += () => Shutdown();

        _hotkeyService = new HotkeyService();
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        RegisterHotkey();
        _settingsService.SettingsChanged += RegisterHotkey;

        if (!_aiService.IsConfigured)
        {
            ShowSettings();
        }
        else if (_hotkeyService.ActiveHotkeyName is { } activeHotkey)
        {
            _trayIcon.ShowNotification("Clap ist bereit",
                $"Text markieren und {activeHotkey} drücken, um den KI-Assistenten zu öffnen.");
            _ = CheckServerAsync();
        }

        // Nach dem Start ungenutzten Speicher freigeben (Hintergrund-Betrieb)
        _ = Dispatcher.InvokeAsync(MemoryTrimmer.Trim,
            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private async void OnHotkeyPressed()
    {
        if (_trayIcon is null || _trayIcon.IsPaused || _captureInProgress) return;

        if (!_aiService.IsConfigured)
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

            // Nur ein Bild, aber kein Vision-Modell → keine anwendbare Aktion
            if (!capture.HasText && capture.HasImage && !_aiService.HasVisionModel)
            {
                _trayIcon.ShowNotification("Clap",
                    "Für die Bildanalyse ist kein Vision-Modell konfiguriert. Bitte in den Einstellungen " +
                    "ein Modell wie \"llava\" hinterlegen (zuvor per \"ollama pull llava\" laden).");
                return;
            }

            var menu = new ActionMenuWindow(capture, _settingsService.Settings.TargetLanguage, _aiService.HasVisionModel);
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

    private void RegisterHotkey()
    {
        if (_hotkeyService is null || _trayIcon is null) return;

        var preferred = _settingsService.Settings.Hotkey;
        if (_hotkeyService.Register(preferred))
        {
            _trayIcon.SetHotkeyText(_hotkeyService.ActiveHotkeyName);
            if (_hotkeyService.ActiveHotkeyName != preferred)
            {
                _trayIcon.ShowNotification("Clap",
                    $"{preferred} ist auf diesem System bereits belegt (z. B. durch Windows). " +
                    $"Clap verwendet stattdessen {_hotkeyService.ActiveHotkeyName}.");
            }
        }
        else
        {
            _trayIcon.SetHotkeyText(null);
            _trayIcon.ShowNotification("Clap",
                "Es konnte kein globaler Shortcut registriert werden – alle Varianten sind belegt.",
                isError: true);
        }
    }

    private async Task CheckServerAsync()
    {
        if (_trayIcon is null) return;
        var models = await _aiService.GetModelsAsync();
        if (models.Count == 0)
        {
            _trayIcon.ShowNotification("Clap",
                $"Der Ollama-Server unter {_settingsService.Settings.OllamaUrl} ist nicht erreichbar. " +
                "Bitte Ollama starten oder die URL in den Einstellungen prüfen.", isError: true);
        }
    }

    private void ShowResult(ClapAction action, CaptureResult capture)
    {
        var window = new ResultWindow(_aiService, action, capture);
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
        if (_ownsMutex)
        {
            try { _singleInstanceMutex?.ReleaseMutex(); }
            catch (ApplicationException) { /* z. B. wenn der Mutex auf einem anderen Thread besessen wird */ }
        }
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
