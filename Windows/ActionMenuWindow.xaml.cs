using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Clap.Models;

namespace Clap.Windows;

/// <summary>Popup-Menü am Mauszeiger mit den verfügbaren KI-Aktionen.</summary>
public partial class ActionMenuWindow : Window
{
    private readonly CaptureResult _capture;
    private bool _closing;

    public event Action<ClapAction>? ActionSelected;

    public ActionMenuWindow(CaptureResult capture, string defaultTargetLanguage, bool visionAvailable)
    {
        InitializeComponent();
        _capture = capture;

        HeaderText.Text = capture.HasText
            ? $"„{Snippet(capture.Text!)}“"
            : "Bild aus der Zwischenablage";

        if (capture.HasText)
        {
            AddSection("Übersetzen");
            foreach (var language in OrderedLanguages(defaultTargetLanguage))
                AddItem(new ClapAction(ClapActionKind.Translate, language));

            AddSection("Verstehen");
            AddItem(new ClapAction(ClapActionKind.Summarize));
            AddItem(new ClapAction(ClapActionKind.Explain));

            AddSection("Umformulieren");
            AddItem(new ClapAction(ClapActionKind.Rephrase, "formell"));
            AddItem(new ClapAction(ClapActionKind.Rephrase, "vereinfacht"));
            AddItem(new ClapAction(ClapActionKind.Rephrase, "prägnant"));
        }

        // Bildanalyse nur anbieten, wenn ein Vision-Modell konfiguriert ist
        if (capture.HasImage && visionAvailable)
        {
            AddSection("Bild");
            AddItem(new ClapAction(ClapActionKind.AnalyzeImage));
        }

        Loaded += (_, _) => PositionAtCursor();
    }

    private static IEnumerable<string> OrderedLanguages(string defaultLanguage)
    {
        string[] all = ["Deutsch", "Englisch", "Tschechisch"];
        return all.OrderBy(l => l == defaultLanguage ? 0 : 1);
    }

    private static string Snippet(string text)
    {
        var compact = text.ReplaceLineEndings(" ").Trim();
        return compact.Length <= 60 ? compact : compact[..60] + "…";
    }

    private void AddSection(string title)
    {
        ItemsPanel.Children.Add(new TextBlock
        {
            Text = title.ToUpperInvariant(),
            Style = (Style)Application.Current.Resources["MenuSection"],
        });
    }

    private void AddItem(ClapAction action)
    {
        var button = new Button
        {
            Content = action.DisplayName,
            Tag = action,
            Style = (Style)Application.Current.Resources["MenuItemButton"],
        };
        button.Click += (_, _) =>
        {
            var selected = (ClapAction)button.Tag;
            // Close() löst zuerst OnDeactivated aus (Fokus geht weg) – das würde
            // erneut Close() rufen und WPF wirft InvalidOperationException
            // ("Window wird bereits geschlossen"). Mit dem Flag fangen wir das ab.
            _closing = true;
            Close();
            ActionSelected?.Invoke(selected);
        };
        ItemsPanel.Children.Add(button);
    }

    private void PositionAtCursor()
    {
        GetCursorPos(out var point);
        var dpi = VisualTreeHelper.GetDpi(this);

        var left = point.X / dpi.DpiScaleX + 4;
        var top = point.Y / dpi.DpiScaleY + 4;

        // Nicht über den Arbeitsbereich hinausragen lassen
        var workArea = SystemParameters.WorkArea;
        if (left + ActualWidth > workArea.Right) left = workArea.Right - ActualWidth - 8;
        if (top + ActualHeight > workArea.Bottom) top = workArea.Bottom - ActualHeight - 8;

        Left = left;
        Top = top;
        Activate();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_closing) return;
        _closing = true;
        Close();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !_closing)
        {
            _closing = true;
            Close();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Win32Point { public int X; public int Y; }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Win32Point point);
}
