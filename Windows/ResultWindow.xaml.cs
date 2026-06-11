using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Clap.Models;
using Clap.Services;

namespace Clap.Windows;

/// <summary>
/// Fortschritts- und Ergebnisdialog: zeigt vor und während der Anfrage einen
/// animierten Status und nimmt die Antwort live aus dem Stream auf.
/// </summary>
public partial class ResultWindow : Window
{
    private readonly OllamaService _ai;
    private readonly ClapAction _action;
    private readonly CaptureResult _capture;
    private readonly CancellationTokenSource _cts = new();
    private readonly Stopwatch _stopwatch = new();
    private readonly DispatcherTimer _elapsedTimer;
    private bool _firstChunkReceived;
    private bool _completedSuccessfully;

    public ResultWindow(OllamaService ai, ClapAction action, CaptureResult capture)
    {
        InitializeComponent();
        _ai = ai;
        _action = action;
        _capture = capture;

        Title = $"Clap – {action.DisplayName}";
        ActionTitle.Text = action.DisplayName;
        SourcePreview.Text = BuildSourcePreview(capture);

        _elapsedTimer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromMilliseconds(100),
        };
        _elapsedTimer.Tick += (_, _) =>
            ElapsedText.Text = $"{_stopwatch.Elapsed.TotalSeconds:0.0} s";

        Loaded += OnLoaded;
        Closed += (_, _) =>
        {
            _cts.Cancel();
            _elapsedTimer.Stop();
            MemoryTrimmer.Trim();
        };
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        StartSpinner();
        _stopwatch.Start();
        _elapsedTimer.Start();
        await RunAsync();
    }

    private static string BuildSourcePreview(CaptureResult capture)
    {
        if (capture.HasImage && !capture.HasText)
            return "Bild aus der Zwischenablage";
        if (capture.Text is null) return string.Empty;
        var compact = capture.Text.ReplaceLineEndings(" ").Trim();
        return compact.Length <= 220 ? compact : compact[..220] + "…";
    }

    private async Task RunAsync()
    {
        try
        {
            await foreach (var chunk in _ai.StreamAsync(_action, _capture, _cts.Token))
            {
                if (!_firstChunkReceived)
                {
                    _firstChunkReceived = true;
                    StatusText.Text = "Modell antwortet…";
                }
                OutputBox.AppendText(chunk);
                OutputBox.ScrollToEnd();
            }

            _stopwatch.Stop();
            _elapsedTimer.Stop();
            _completedSuccessfully = true;
            FinishUi(success: true,
                $"Fertig – {_stopwatch.Elapsed.TotalSeconds:0.0} s");
            CopyButton.IsEnabled = OutputBox.Text.Length > 0;
        }
        catch (OperationCanceledException)
        {
            // Fenster wurde vorzeitig geschlossen → nichts zu tun
        }
        catch (Exception ex)
        {
            _stopwatch.Stop();
            _elapsedTimer.Stop();
            FinishUi(success: false, "Fehler bei der Anfrage");
            if (OutputBox.Text.Length > 0) OutputBox.AppendText("\n\n");
            OutputBox.AppendText($"⚠ {ex.Message}");
        }
    }

    /// <summary>Versteckt Spinner und Fortschrittsbalken; Buttontext anpassen.</summary>
    private void FinishUi(bool success, string status)
    {
        StatusText.Text = status;
        StatusText.Foreground = success
            ? System.Windows.Media.Brushes.LightGreen
            : System.Windows.Media.Brushes.Salmon;
        Spinner.Visibility = Visibility.Collapsed;
        ProgressIndicator.Visibility = Visibility.Collapsed;
        CloseButton.Content = "Schließen";

        // Erste Antwort anzeigen, dann StartUpdate nicht mehr wiederholen
        StopSpinner();
    }

    /// <summary>Stoppen-Variante: einfach Cancellation triggern und Fenster zu.</summary>
    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (OutputBox.Text.Length == 0) return;
        Clipboard.SetText(OutputBox.Text);
        StatusText.Text = "In die Zwischenablage kopiert";
        StatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && _completedSuccessfully
                 && OutputBox.SelectionLength == 0)
        {
            // Strg+C ohne Auswahl → komplettes Ergebnis kopieren
            OnCopyClick(sender, e);
            e.Handled = true;
        }
    }

    private Storyboard? _spinnerStoryboard;

    private void StartSpinner()
    {
        var storyboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };

        AddPulse(storyboard, "Dot1Scale", beginMs: 0);
        AddPulse(storyboard, "Dot2Scale", beginMs: 150);
        AddPulse(storyboard, "Dot3Scale", beginMs: 300);

        storyboard.Begin(this, isControllable: true);
        _spinnerStoryboard = storyboard;
    }

    private static void AddPulse(Storyboard storyboard, string targetName, int beginMs)
    {
        foreach (var prop in new[] { "(ScaleTransform.ScaleX)", "(ScaleTransform.ScaleY)" })
        {
            var animation = new DoubleAnimationUsingKeyFrames
            {
                BeginTime = TimeSpan.FromMilliseconds(beginMs),
                Duration = new Duration(TimeSpan.FromMilliseconds(900)),
            };
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(0.6, KeyTime.FromPercent(0.0)));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(1.4, KeyTime.FromPercent(0.5)));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(0.6, KeyTime.FromPercent(1.0)));

            Storyboard.SetTargetName(animation, targetName);
            Storyboard.SetTargetProperty(animation, new PropertyPath(prop));
            storyboard.Children.Add(animation);
        }
    }

    private void StopSpinner()
    {
        _spinnerStoryboard?.Stop(this);
        _spinnerStoryboard = null;
    }
}
