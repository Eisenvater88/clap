# Testkonzept – Clap

Clap ist ein Windows-Tray-Assistent (WPF/.NET 8), der markierten Text bzw. Bilder
aus der Zwischenablage erfasst und über einen lokalen **Ollama**-Server verarbeitet
(Übersetzen, Zusammenfassen, Erklären, Umformulieren, Korrigieren, Bildanalyse).

Dieses Dokument beschreibt die Teststrategie und den aktuellen Umfang der automatisierten Tests.

## 1. Ziele

- **Regressionsschutz** für die reine Geschäftslogik – insbesondere die fehleranfälligen
  Teile (Stream-Filter, Prompt-Aufbau, Einstellungs-Migration).
- **Schnelle, deterministische** Tests ohne Netzwerk, ohne laufenden Ollama-Server und
  ohne UI-Interaktion.
- Tests laufen lokal (`dotnet test`) und sind CI-tauglich.

## 2. Teststrategie (Testpyramide)

Der Code ist klar in zwei Schichten geteilt:

| Schicht | Beispiele | Testbarkeit |
|---|---|---|
| **Reine Logik** | `OllamaService.BuildRequest`/`ThinkFilter`/`ExtractError`, `SettingsService.ApplyMigrations`, `ClapAction`, `CaptureResult`, `AppSettings`, `HotkeyService.Options` | Unit-Tests (automatisiert) |
| **Plattform-/IO-gebunden** | Win32 `SendInput`/Clipboard, `RegisterHotKey`, Registry-Autostart, HTTP-Streaming, WPF-Fenster | Manuell / nicht unit-getestet |

Der Schwerpunkt liegt bewusst auf der Basis der Pyramide: **viele schnelle Unit-Tests**
für die Logik. Plattformgebundene Adapter werden manuell verifiziert (siehe §6).

## 3. Werkzeuge

- **xUnit** als Test-Framework, **Microsoft.NET.Test.Sdk** + **xunit.runner.visualstudio** als Runner.
- Keine Mocking-Bibliothek nötig: Abhängigkeiten werden über echte, leichtgewichtige
  Objekte gestellt (`SettingsService` mit gesetzten `Settings`).
- Testprojekt: [`tests/Clap.Tests/`](tests/Clap.Tests/Clap.Tests.csproj), eingebunden über [`Clap.sln`](Clap.sln).

Damit Tests auf interne Logik zugreifen können, ist im Hauptprojekt
`[assembly: InternalsVisibleTo("Clap.Tests")]` gesetzt; einige Member sind `internal`
statt `private` (z. B. `ThinkFilter`, `BuildRequest`, `ExtractError`, `ApplyMigrations`).
Es wurde **keine** öffentliche API nur für Tests aufgeweicht.

## 4. Ausführung

```bash
dotnet test                       # gesamte Solution
dotnet test tests/Clap.Tests      # nur die Testsuite
```

Voraussetzung: .NET 8 SDK. Die Tests benötigen **keinen** Ollama-Server und keine Netzwerkverbindung.

## 5. Abgedeckte Bereiche (Unit-Tests)

| Datei | Geprüfte Logik |
|---|---|
| `Services/ThinkFilterTests.cs` | Entfernen von `<think>…</think>`-Blöcken aus dem Stream, inkl. über Chunk-Grenzen zerschnittener Tags, Zeichen-für-Zeichen-Streaming, ungeschlossener Blöcke, echter `<`-Zeichen ohne Tag. |
| `Services/OllamaServiceTests.cs` | `IsConfigured`/`HasVisionModel`; `BuildRequest`: Modellauswahl, System-Prompts je Aktion, Stil-Leitfaden nur beim Umformulieren, Bild-/Vision-Handling, Fehlerfälle (kein Modell/Text/Bild); `ExtractError` (JSON/Rohtext/leer). |
| `Services/SettingsMigrationTests.cs` | Migration alter „claude…"-Modellnamen → leer; Ollama-Namen bleiben unangetastet. |
| `Services/HotkeyServiceTests.cs` | Inhalt & Eindeutigkeit der Shortcut-Optionen, Fallback-Reihenfolge (bevorzugter Shortcut zuerst). |
| `Models/ClapActionTests.cs` | `DisplayName` je Aktionsart, Record-Wertgleichheit. |
| `Models/CaptureResultTests.cs` | `HasText`/`HasImage`/`IsEmpty` inkl. Whitespace-Grenzfälle. |
| `Models/AppSettingsTests.cs` | Standardwerte, JSON-Round-Trip, Defaults bei unvollständigem JSON. |

Aktueller Stand: **58 Tests, alle grün**.

## 6. Bewusst nicht (automatisch) abgedeckt

Diese Teile sind eng an Windows-APIs, Hardware-Eingaben oder UI gekoppelt und werden
**manuell** getestet:

- **`TextCaptureService`** – simuliertes Strg+C via `SendInput`, Clipboard-Sicherung/-Wiederherstellung.
- **`HotkeyService.Register`** – Win32 `RegisterHotKey` inkl. tatsächlichem Ausweichen auf Alternativen.
- **`AutostartService`** – Schreiben/Löschen im HKCU-Run-Registry-Key.
- **`TrayIconService`** – NotifyIcon/Tray-Menü.
- **`OllamaService.StreamAsync`/HTTP** – echtes NDJSON-Streaming, Thinking-Retry (400 → ohne `think`).
- **WPF-Fenster** (`ActionMenuWindow`, `ResultWindow`, `SettingsWindow`) – Layout, Positionierung am Cursor, Fokus/Schließverhalten.

### Manuelle Smoke-Test-Checkliste vor einem Release

1. App starten → Tray-Icon erscheint, Hotkey ist registriert (Tooltip zeigt aktiven Shortcut).
2. Text markieren → Hotkey → Menü erscheint am Cursor; Zwischenablage bleibt danach unverändert.
3. Je eine Aktion ausführen: Übersetzen / Zusammenfassen / Erklären / Korrigieren / Umformulieren.
4. Bild in die Zwischenablage kopieren → Hotkey → „Bild analysieren" (nur wenn Vision-Modell gesetzt).
5. Einstellungen: Ollama-URL/Modell ändern, Stil-Leitfaden setzen → Umformulieren nutzt ihn, Übersetzen nicht.
6. Ollama gestoppt → Aktion zeigt verständliche Fehlermeldung statt Absturz.
7. Autostart an/aus → Registry-Eintrag „Clap" vorhanden/entfernt.

## 7. Mögliche Erweiterungen

- Logik aus den WPF-Fenstern extrahieren (z. B. `OrderedLanguages`, `Snippet` aus
  `ActionMenuWindow`) in testbare Hilfsklassen.
- `OllamaService.StreamAsync` über einen einschleusbaren `HttpMessageHandler` testbar
  machen (Streaming-/Retry-Pfad ohne echten Server).
- CI-Workflow (GitHub Actions, `windows-latest`) mit `dotnet test`.
