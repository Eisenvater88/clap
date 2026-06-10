using System.Windows;
using System.Windows.Controls;
using Clap.Services;

namespace Clap.Windows;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;

    public SettingsWindow(SettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;

        var settings = settingsService.Settings;

        if (!string.IsNullOrEmpty(settingsService.GetApiKey()))
            ApiKeyHint.Text = "Ein API-Key ist hinterlegt. Feld leer lassen, um ihn zu behalten.";

        SelectByTag(ModelBox, settings.Model);
        SelectByContent(LanguageBox, settings.TargetLanguage);
        AutostartBox.IsChecked = settings.Autostart;
    }

    private static void SelectByTag(ComboBox box, string tag)
    {
        box.SelectedIndex = 0;
        foreach (ComboBoxItem item in box.Items)
        {
            if ((string)item.Tag == tag) { box.SelectedItem = item; return; }
        }
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

        var enteredKey = ApiKeyBox.Password.Trim();
        if (enteredKey.Length > 0)
            _settingsService.SetApiKey(enteredKey);

        if (string.IsNullOrEmpty(_settingsService.GetApiKey()))
        {
            MessageBox.Show(this, "Bitte einen Anthropic API-Key eingeben.", "Clap",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        settings.Model = (string)((ComboBoxItem)ModelBox.SelectedItem).Tag;
        settings.TargetLanguage = (string)((ComboBoxItem)LanguageBox.SelectedItem).Content;
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
