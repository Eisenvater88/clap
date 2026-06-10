# Clap – systemweiter KI-Assistent für Windows

Clap läuft als Tray-App im Hintergrund. Markierten Text in **jeder Anwendung** per
**Strg + Win + C** an Claude übergeben: Übersetzen, Zusammenfassen, Erklären,
Umformulieren oder Bilder aus der Zwischenablage analysieren.

## Funktionsweise

1. Text in einer beliebigen Anwendung markieren (oder Screenshot in die Zwischenablage legen)
2. **Strg + Win + C** drücken
3. Im Popup-Menü die gewünschte Aktion wählen
4. Das Ergebnis erscheint live gestreamt in einem Fenster und kann kopiert werden

Die Zwischenablage des Nutzers wird bei der Erfassung gesichert und anschließend
wiederhergestellt — vorhandene Inhalte gehen nicht verloren.

## Einrichtung

1. Build: `dotnet build -c Release` (benötigt .NET 8 SDK)
2. `bin\Release\net8.0-windows\Clap.exe` starten
3. Beim ersten Start öffnen sich die Einstellungen: **Anthropic API-Key** eintragen
   (wird per DPAPI verschlüsselt unter `%APPDATA%\Clap\settings.json` gespeichert)
4. Optional: Modell, Standard-Zielsprache und Autostart anpassen

## Einstellungen

| Option | Beschreibung |
|---|---|
| API-Key | Anthropic-Key, DPAPI-verschlüsselt (nur für das aktuelle Windows-Konto lesbar) |
| Modell | Opus 4.8 (beste Qualität), Sonnet 4.6 (ausgewogen), Haiku 4.5 (am schnellsten) |
| Zielsprache | Standardziel für Übersetzungen (Deutsch / Englisch / Tschechisch) |
| Autostart | Start mit Windows-Login (HKCU-Run-Key, keine Adminrechte nötig) |

## Tray-Icon

- **Orange C** = aktiv, **graues C** = pausiert
- Rechtsklick: Pausieren / Einstellungen / Beenden
- Doppelklick: Einstellungen

## Technik

- C# / WPF auf .NET 8, offizielles Anthropic C# SDK (Streaming über die Messages API)
- Globaler Hotkey über `RegisterHotKey` (Message-Only-Window)
- Texterfassung per simuliertem Strg+C (`SendInput`) mit Sicherung/Wiederherstellung der Zwischenablage
- Ressourcenverbrauch im Leerlauf: ~20 MB Working Set, 0 % CPU
