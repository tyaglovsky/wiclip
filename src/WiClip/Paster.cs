using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace WiClip;

/// <summary>
/// Что именно кладём в буфер. Общий вид для записи истории и записи библиотеки —
/// у них разные хранилища, но одинаковый набор данных для Windows.
/// </summary>
public readonly record struct ClipboardPayload(
    ClipKind Kind,
    string Text,
    string? ImagePath = null,
    IReadOnlyList<string>? Files = null);

/// <summary>Кладёт запись в буфер обмена и (по желанию) шлёт Ctrl+V в целевое окно.</summary>
public static class Paster
{
    private const int SW_RESTORE = 9;

    /// <summary>Положить запись в буфер обмена. Возвращает false при неудаче.</summary>
    public static bool CopyToClipboard(ClipboardPayload item)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                switch (item.Kind)
                {
                    case ClipKind.Image:
                        if (item.ImagePath is null || !File.Exists(item.ImagePath)) return false;
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.UriSource = new Uri(item.ImagePath);
                        bmp.EndInit();
                        Clipboard.SetImage(bmp);
                        break;

                    case ClipKind.Files:
                        var files = new StringCollection();
                        foreach (var path in item.Files ?? Array.Empty<string>())
                        {
                            if (File.Exists(path) || Directory.Exists(path)) files.Add(path);
                        }
                        if (files.Count == 0) return false;
                        Clipboard.SetFileDropList(files);
                        break;

                    default:
                        // copy: true — данные остаются в буфере после выхода из приложения.
                        Clipboard.SetDataObject(item.Text, copy: true);
                        break;
                }

                Clipboard.Flush();
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn($"Clipboard busy while writing ({ex.GetType().Name}), attempt {attempt + 1}.");
                Thread.Sleep(80);
            }
        }

        Log.Error("Could not write to the clipboard.");
        return false;
    }

    /// <summary>Вернуть фокус целевому окну и отправить ему Ctrl+V.</summary>
    public static void PasteInto(IntPtr target)
    {
        if (target == IntPtr.Zero) return;

        try
        {
            if (Native.IsIconic(target)) Native.ShowWindow(target, SW_RESTORE);

            // Без AttachThreadInput Windows часто запрещает смену активного окна.
            var ourThread = Native.GetCurrentThreadId();
            var targetThread = Native.GetWindowThreadProcessId(target, out _);
            var attached = targetThread != ourThread &&
                           Native.AttachThreadInput(ourThread, targetThread, true);

            Native.SetForegroundWindow(target);

            if (attached) Native.AttachThreadInput(ourThread, targetThread, false);

            // Небольшая пауза: окно должно успеть получить фокус ввода.
            Thread.Sleep(60);

            var inputs = new[]
            {
                Native.Key(Native.VK_CONTROL, up: false),
                Native.Key(Native.VK_V, up: false),
                Native.Key(Native.VK_V, up: true),
                Native.Key(Native.VK_CONTROL, up: true)
            };

            var sent = Native.SendInput((uint)inputs.Length, inputs,
                System.Runtime.InteropServices.Marshal.SizeOf<Native.INPUT>());

            if (sent != inputs.Length)
                Log.Warn($"SendInput delivered {sent} of {inputs.Length} events " +
                         "(the target window may run elevated).");
        }
        catch (Exception ex)
        {
            Log.Error($"Paste failed: {ex.Message}");
        }
    }
}
