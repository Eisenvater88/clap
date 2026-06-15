using System.Windows;
using System.Windows.Controls;
using Clap.Services;

namespace Clap.Windows;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly OllamaService _ai;

    public SettingsWindow(SettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _ai = new OllamaService(settingsService);

        var settings = settingsService.Settings;

        UrlBox.Text = settings.OllamaUrl;
        ModelBox.Text = settings.Model;
        VisionModelBox.Text = settings.VisionModel;

        SelectByContent(LanguageBox, settings.TargetLanguage);

        StyleGuideBox.Text = settings.RephraseStyleGuide;

        foreach (var option in HotkeyService.Options)
            HotkeyBox.Items.Add(new ComboBoxItem { Content = option.Name });
        SelectByContent(HotkeyBox, settings.Hotkey);

        AutostartBox.IsChecked = settings.Autostart;

        Loaded += async (_, _) => await LoadModelsAsync();
    }

    /// <summary>Lädt die auf dem Server installierten Modelle in die Auswahlfelder.</summary>
    private async Task LoadModelsAsync()
    {
        // Aktuelle URL zur Abfrage verwenden
        _settingsService.Settings.OllamaUrl = UrlBox.Text.Trim();

        var models = await _ai.GetModelsAsync();

        var currentModel = ModelBox.Text;
        var currentVision = VisionModelBox.Text;

        ModelBox.Items.Clear();
        VisionModelBox.Items.Clear();
        VisionModelBox.Items.Add(new ComboBoxItem { Content = "" }); // „kein Vision-Modell"
        foreach (var model in models)
        {
            ModelBox.Items.Add(model);
            VisionModelBox.Items.Add(model);
        }

        // Bisherige Auswahl beibehalten; sonst erstes verfügbares Textmodell vorschlagen
        ModelBox.Text = !string.IsNullOrWhiteSpace(currentModel) ? currentModel
            : models.FirstOrDefault() ?? "";
        VisionModelBox.Text = currentVision;

        if (models.Count > 0)
            ConnectionHint.Text = $"Verbunden – {models.Count} Modell(e) gefunden.";
        else
            ConnectionHint.Text = "Keine Verbindung / keine Modelle. Läuft Ollama unter dieser URL?";
    }

    private async void OnTestClick(object sender, RoutedEventArgs e)
    {
        ConnectionHint.Text = "Verbinde…";
        await LoadModelsAsync();
    }

    private static void SelectByContent(ComboBox box, string content)
    {
        box.SelectedIndex = 0;
        foreach (ComboBoxItem item in box.Items)
        {
            if ((string)item.Content == content) { box.SelectedItem = item; return; }
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.Settings;

        var url = UrlBox.Text.Trim();
        if (url.Length == 0)
        {
            MessageBox.Show(this, "Bitte die URL des Ollama-Servers angeben.", "Clap",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var model = ModelBox.Text.Trim();
        if (model.Length == 0)
        {
            MessageBox.Show(this, "Bitte ein Textmodell auswählen oder eingeben.", "Clap",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        settings.OllamaUrl = url;
        settings.Model = model;
        settings.VisionModel = VisionModelBox.Text.Trim();
        settings.TargetLanguage = (string)((ComboBoxItem)LanguageBox.SelectedItem).Content;
        settings.RephraseStyleGuide = StyleGuideBox.Text.Trim();
        settings.Hotkey = (string)((ComboBoxItem)HotkeyBox.SelectedItem).Content;
        settings.Autostart = AutostartBox.IsChecked == true;

        try
        {
            AutostartService.Apply(settings.Autostart);
        }
        catch
        {
            // Registry nicht beschreibbar (z. B. Richtlinie) — Einstellung trotzdem speichern
        }

        _settingsService.Save();
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
}
