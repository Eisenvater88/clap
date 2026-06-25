# Clap – systemweiter KI-Assistent für Windows

Clap läuft als Tray-App im Hintergrund. Markierten Text in **jeder Anwendung** per
Tastenkürzel an ein **lokales Ollama-Modell** übergeben: Übersetzen, Zusammenfassen,
Erklären, Rechtschreibung & Grammatik korrigieren, Umformulieren oder Bilder aus der
Zwischenablage analysieren. Alle Daten
bleiben auf dem Rechner (bzw. auf dem konfigurierten Ollama-Server) — kein Cloud-Versand.

## Funktionsweise

1. Text in einer beliebigen Anwendung markieren (oder Screenshot in die Zwischenablage legen)
2. **Strg + Win + C** drücken (ist der Shortcut belegt, weicht Clap automatisch aus — siehe unten)
3. Im Popup-Menü die gewünschte Aktion wählen
4. Das Ergebnis erscheint live gestreamt in einem Fenster und kann kopiert werden

Die Zwischenablage des Nutzers wird bei der Erfassung gesichert und anschließend
wiederhergestellt — vorhandene Inhalte gehen nicht verloren.

## Voraussetzungen

- **Ollama** installiert und gestartet ([ollama.com](https://ollama.com))
- Mindestens ein Textmodell, z. B. `ollama pull qwen3:4b`
- Optional für die Bildanalyse ein Vision-Modell, z. B. `ollama pull llava`

## Einrichtung

1. Build: `dotnet build -c Release` (benötigt .NET 8 SDK, keine externen NuGet-Pakete)
2. `bin\Release\net8.0-windows\Clap.exe` starten
3. Beim ersten Start öffnen sich die Einstellungen:
   - **Ollama-Server** prüfen (lokal `http://localhost:11434`) → „Verbinden" lädt die installierten Modelle
   - **Textmodell** wählen, optional **Vision-Modell**
4. Optional: Zielsprache, Ausgabeformat, Shortcut und Autostart anpassen

### Persönlicher Stil für Umformulieren (optional)

Im Feld **„Persönlicher Stil (nur beim Umformulieren)"** lässt sich ein eigener
Stil-Leitfaden als **Markdown** hinterlegen: bevorzugte Wortwahl, häufig genutzte
Formulierungen und Wörter, die nie verwendet werden sollen. Diese Vorgaben fließen
**ausschließlich** in die **Umformulieren**-Aktionen ein, damit das Ergebnis
persönlicher statt generisch klingt – beim Übersetzen, Zusammenfassen, Erklären und
bei der Bildanalyse bleiben sie wirkungslos.

Beispiel:

```markdown
# Mein Stil
- kurze, aktive Sätze
- bevorzugt: „Servus", „passt", „melde mich"
- nie nutzen: „diesbezüglich", „seitens", „bezüglich"
```

Das Feld leer lassen, um den generischen Stil beizubehalten.

### Ausgabeformat (Markdown / Klartext)

Über die Einstellung **„Ausgabeformat"** lässt sich steuern, wie viel
Markdown-Formatierung die KI-Antwort enthält – praktisch für Copy-und-Paste in
Programme, die Markdown nicht interpretieren (E-Mails, Ticketsysteme, Office, Chats)
und sonst Zeichen wie `**` oder `#` sichtbar einfügen:

- **Klartext (ohne Markdown)** – *Standard*: Fett, Kursiv, Überschriften, Links und
  Aufzählungszeichen werden entfernt; es bleibt gut lesbarer Klartext.
- **Klartext mit Struktur** – Aufzählungen und Absätze bleiben erhalten, nur Fett,
  Kursiv und Überschriften werden entfernt.
- **Markdown (Formatierung beibehalten)** – die Antwort wird unverändert ausgegeben.

Die Umwandlung erfolgt rein auf der Ausgabe (kein Eingriff in den Prompt) und wirkt
auf **alle** Aktionen. Hotkey, Zwischenablage und Anzeige bleiben unverändert.

### Korrigieren vs. Umformulieren

Die Aktion **„Rechtschreibung & Grammatik korrigieren"** ändert ausschließlich
Rechtschreibung, Grammatik und Zeichensetzung – Wortwahl, Satzbau, Stil, Ton und
Bedeutung bleiben unangetastet. Ist der Text bereits fehlerfrei, wird er unverändert
zurückgegeben. Wer den Text bewusst umschreiben lassen möchte, nutzt stattdessen die
**Umformulieren**-Aktionen. Der persönliche Stil-Leitfaden greift nur beim Umformulieren,
nicht beim Korrigieren.

## Einstellungen

| Option | Beschreibung |
|---|---|
| Ollama-Server | Basis-URL; lokal oder im Netz (z. B. zentraler Server / VDI-Host) |
| Textmodell | Modell für Übersetzen/Zusammenfassen/Erklären/Korrigieren/Umformulieren |
| Vision-Modell | Optional für Bildanalyse; leer = Funktion ausgeblendet |
| Zielsprache | Standardziel für Übersetzungen (Deutsch / Englisch / Tschechisch) |
| Ausgabeformat | Klartext (Standard) / Klartext mit Struktur / Markdown – entfernt bei Bedarf Markdown für sauberes Copy & Paste |
| Persönlicher Stil | Optionaler Markdown-Leitfaden; greift nur beim Umformulieren, leer = generisch |
| Shortcut | Strg+Win+C / Strg+Alt+C / Strg+Win+Y mit automatischem Fallback |
| Autostart | Start mit Windows-Login (HKCU-Run-Key, keine Adminrechte nötig) |

Die Einstellungen liegen unter `%APPDATA%\Clap\settings.json`. Dort lässt sich auch
das Kontextfenster anpassen (`NumCtx`, Standard 8192): Modelle wie qwen3 haben ein sehr
großes Standard-Kontextfenster, das sonst den Arbeitsspeicher sprengt. Höhere Werte
erlauben längere Texte, brauchen aber mehr RAM.

## Tray-Icon

- **Orange C** = aktiv, **graues C** = pausiert
- Rechtsklick: Pausieren / Einstellungen / Beenden
- Doppelklick: Einstellungen
- Tooltip zeigt den aktiven Shortcut

## Technik

- C# / WPF auf .NET 8, keine externen Pakete (Ollama via `HttpClient` + `System.Text.Json`)
- Ollama-Anbindung über die native `/api/chat`-Schnittstelle, NDJSON-Streaming
- „Thinking"-Modelle (z. B. qwen3): `think:true` trennt den Denkprozess ab, ein Filter
  entfernt zusätzlich etwaige `<think>…</think>`-Blöcke aus der Ausgabe
- Globaler Hotkey über `RegisterHotKey` (Message-Only-Window) mit Fallback-Varianten
- Texterfassung per simuliertem Strg+C (`SendInput`) mit Sicherung/Wiederherstellung der Zwischenablage
- Ressourcenverbrauch im Leerlauf: ~20–45 MB Working Set, 0 % CPU (Modell läuft im Ollama-Prozess, nicht in Clap)
