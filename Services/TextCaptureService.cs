using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Clap.Models;

namespace Clap.Services;

/// <summary>
/// Erfasst den aktuell markierten Text per simuliertem Strg+C und stellt die
/// Zwischenablage des Nutzers anschließend wieder her.
/// </summary>
public static class TextCaptureService
{
    public static async Task<CaptureResult> CaptureAsync()
    {
        // 1. Zwischenablage sichern (Text, Bild, Dateiliste)
        string? backupText = null;
        BitmapSource? backupImage = null;
        StringCollection? backupFiles = null;
        TryClipboard(() =>
        {
            if (Clipboard.ContainsText()) backupText = Clipboard.GetText();
            if (Clipboard.ContainsImage()) backupImage = Clipboard.GetImage();
            if (Clipboard.ContainsFileDropList()) backupFiles = Clipboard.GetFileDropList();
        });

        var sequenceBefore = GetClipboardSequenceNumber();

        // 2. Vom Nutzer noch gehaltene Modifier lösen, dann Strg+C senden
        SendKeyUp(VkLWin);
        SendKeyUp(VkRWin);
        SendKeyUp(VkControl);
        await Task.Delay(30);
        SendCtrlC();

        // 3. Warten, bis die Ziel-Anwendung die Zwischenablage befüllt hat (max. 600 ms)
        var clipboardChanged = false;
        for (var i = 0; i < 12; i++)
        {
            await Task.Delay(50);
            if (GetClipboardSequenceNumber() != sequenceBefore)
            {
                clipboardChanged = true;
                break;
            }
        }

        // 4. Inhalt lesen
        string? capturedText = null;
        BitmapSource? capturedImage = null;
        TryClipboard(() =>
        {
            if (clipboardChanged && Clipboard.ContainsText())
                capturedText = Clipboard.GetText();
            // Bild: entweder frisch kopiert oder bereits vorher in der Zwischenablage (z. B. Screenshot)
            if (Clipboard.ContainsImage())
                capturedImage = Clipboard.GetImage();
        });

        // 5. Zwischenablage wiederherstellen, falls wir sie verändert haben
        if (clipboardChanged)
        {
            TryClipboard(() =>
            {
                if (backupText is null && backupImage is null && backupFiles is null)
                {
                    Clipboard.Clear();
                    return;
                }
                var data = new DataObject();
                if (backupText is not null) data.SetText(backupText);
                if (backupImage is not null) data.SetImage(backupImage);
                if (backupFiles is not null) data.SetFileDropList(backupFiles);
                Clipboard.SetDataObject(data, copy: true);
            });
        }

        return new CaptureResult
        {
            Text = string.IsNullOrWhiteSpace(capturedText) ? null : capturedText,
            ImagePngBase64 = capturedImage is null ? null : EncodePng(capturedImage),
        };
    }

    private static string EncodePng(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return Convert.ToBase64String(stream.ToArray());
    }

    /// <summary>Zwischenablage-Zugriffe können fehlschlagen, wenn sie gerade gesperrt ist — mit Retry.</summary>
    private static void TryClipboard(Action action)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (COMException)
            {
                Thread.Sleep(40);
            }
        }
    }

    #region Win32 SendInput

    private const ushort VkControl = 0x11;
    private const ushort VkLWin = 0x5B;
    private const ushort VkRWin = 0x5C;
    private const ushort VkC = 0x43;
    private const uint KeyEventFKeyUp = 0x0002;
    private const uint InputKeyboard = 1;

    private static void SendCtrlC()
    {
        var inputs = new[]
        {
            KeyInput(VkControl, false),
            KeyInput(VkC, false),
            KeyInput(VkC, true),
            KeyInput(VkControl, true),
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static void SendKeyUp(ushort vk)
    {
        var inputs = new[] { KeyInput(vk, true) };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static Input KeyInput(ushort vk, bool keyUp) => new()
    {
        Type = InputKeyboard,
        Union = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = vk,
                Flags = keyUp ? KeyEventFKeyUp : 0,
            },
        },
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    #endregion
}
