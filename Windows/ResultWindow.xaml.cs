using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Clap.Models;
using Clap.Services;

namespace Clap.Windows;

/// <summary>Zeigt das KI-Ergebnis als Live-Stream an.</summary>
public partial class ResultWindow : Window
{
    private readonly ClaudeService _claude;
    private readonly ClapAction _action;
    private readonly CaptureResult _capture;
    private readonly CancellationTokenSource _cts = new();

    public ResultWindow(ClaudeService claude, ClapAction action, CaptureResult capture)
    {
        InitializeComponent();
        _claude = claude;
        _action = action;
        _capture = capture;

        Title = $"Clap – {action.DisplayName}";
        Loaded += async (_, _) => await RunAsync();
        Closed += (_, _) =>
        {
            _cts.Cancel();
            MemoryTrimmer.Trim();
        };
    }

    private async Task RunAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await foreach (var chunk in _claude.StreamAsync(_action, _capture, _cts.Token))
            {
                OutputBox.AppendText(chunk);
                OutputBox.ScrollToEnd();
            }
            stopwatch.Stop();
            StatusText.Text = $"{_action.DisplayName} – fertig ({stopwatch.Elapsed.TotalSeconds:F1} s)";
            CopyButton.IsEnabled = OutputBox.Text.Length > 0;
        }
        catch (OperationCanceledException)
        {
            // Fenster wurde geschlossen
        }
        catch (Exception ex)
        {
            StatusText.Text = "Fehler bei der Anfrage";
            OutputBox.AppendText($"\n\n⚠ {ex.Message}");
        }
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (OutputBox.Text.Length == 0) return;
        Clipboard.SetText(OutputBox.Text);
        StatusText.Text = "In die Zwischenablage kopiert";
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }
}
