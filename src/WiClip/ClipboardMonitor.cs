using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WiClip;

/// <summary>Слушает изменения буфера обмена и складывает их в историю.</summary>
public sealed class ClipboardMonitor : IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly HistoryStore _store;
    private readonly AppSettings _settings;

    private readonly uint _fmtViewerIgnore;
    private readonly uint _fmtExcludeMonitor;
    private readonly uint _fmtCanIncludeHistory;

    private readonly DispatcherTimer _pollTimer;

    private DateTime _suppressUntil = DateTime.MinValue;
    private uint _lastSequence;
    private int _generation;
    private bool _disposed;

    public ClipboardMonitor(IntPtr hwnd, HistoryStore store, AppSettings settings)
    {
        _hwnd = hwnd;
        _store = store;
        _settings = settings;

        _fmtViewerIgnore = Native.RegisterClipboardFormat("Clipboard Viewer Ignore");
        _fmtExcludeMonitor = Native.RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
        _fmtCanIncludeHistory = Native.RegisterClipboardFormat("CanIncludeInClipboardHistory");

        if (Native.AddClipboardFormatListener(_hwnd))
            Log.Info($"Слушатель буфера обмена подключён (hwnd 0x{hwnd.ToInt64():X}).");
        else
            Log.Error($"AddClipboardFormatListener не сработал (код {Marshal.GetLastWin32Error()}), " +
                      "работаем только на опросе.");

        _lastSequence = Native.GetClipboardSequenceNumber();

        // Резерв на случай, если сообщения не доходят: буфер меняется — меняется и счётчик.
        _pollTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _pollTimer.Tick += (_, _) => CheckSequence();
        _pollTimer.Start();
    }

    /// <summary>Не записывать в историю то, что мы сами кладём в буфер при вставке.</summary>
    public void SuppressNext(TimeSpan? window = null) =>
        _suppressUntil = DateTime.UtcNow + (window ?? TimeSpan.FromMilliseconds(700));

    public bool HandleMessage(int msg)
    {
        if (msg != Native.WM_CLIPBOARDUPDATE) return false;
        _lastSequence = Native.GetClipboardSequenceNumber();
        Log.Info("WM_CLIPBOARDUPDATE: буфер изменился.");
        _ = CaptureAsync(++_generation);
        return true;
    }

    private void CheckSequence()
    {
        var seq = Native.GetClipboardSequenceNumber();
        if (seq == _lastSequence) return;

        _lastSequence = seq;
        Log.Info($"Опрос: счётчик буфера изменился ({seq}).");
        _ = CaptureAsync(++_generation);
    }

    private async Task CaptureAsync(int generation)
    {
        if (DateTime.UtcNow < _suppressUntil)
        {
            Log.Info("Пропуск: это наша собственная запись в буфер.");
            return;
        }

        var (sourceApp, ownerHwnd) = GetClipboardOwnerApp();
        if (IsIgnoredProcess(sourceApp))
        {
            Log.Info($"Пропуск буфера из процесса {sourceApp} (в списке игнорируемых).");
            return;
        }

        if (_settings.RespectSecretClipboard && IsMarkedSecret())
        {
            Log.Info("Пропуск буфера, помеченного как секретный.");
            return;
        }

        // Буфер часто ещё занят приложением-источником: пробуем несколько раз.
        for (var attempt = 0; attempt < 6; attempt++)
        {
            if (generation != _generation) return; // пришло более свежее обновление
            try
            {
                var item = ReadClipboard(sourceApp, ownerHwnd);
                if (item is null)
                {
                    Log.Info("В буфере нет данных подходящего формата — пропуск.");
                    return;
                }

                _store.Add(item);
                Log.Info($"Добавлено в историю: {item.Kind}, источник «{item.SourceApp}». " +
                         $"Всего записей: {_store.Items.Count}.");
                return;
            }
            catch (Exception ex) when (attempt < 5)
            {
                Log.Warn($"Буфер занят ({ex.GetType().Name}), попытка {attempt + 1}.");
                await Task.Delay(120);
            }
            catch (Exception ex)
            {
                Log.Error($"Не удалось прочитать буфер: {ex.Message}");
                return;
            }
        }
    }

    private ClipItem? ReadClipboard(string sourceApp, IntPtr ownerHwnd)
    {
        var title = Native.GetWindowTitle(ownerHwnd);
        var app = string.IsNullOrEmpty(sourceApp) ? title : sourceApp;

        if (Clipboard.ContainsText())
        {
            var text = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (text.Length > _settings.MaxTextLength)
            {
                Log.Info($"Текст {text.Length} симв. — длиннее лимита, не сохраняем.");
                return null;
            }
            return new ClipItem { Kind = ClipKind.Text, Text = text, SourceApp = app };
        }

        if (Clipboard.ContainsFileDropList())
        {
            var files = Clipboard.GetFileDropList().Cast<string>().Where(f => f is not null).ToArray();
            if (files.Length == 0) return null;
            return new ClipItem
            {
                Kind = ClipKind.Files,
                Text = string.Join("\n", files),
                SourceApp = app
            };
        }

        if (_settings.CaptureImages && Clipboard.ContainsImage())
        {
            var image = Clipboard.GetImage();
            if (image is null) return null;

            var item = new ClipItem { Kind = ClipKind.Image, SourceApp = app };
            item.ImageFile = SaveImage(image, item.Id);
            if (item.ImageFile is null) return null;
            item.Text = $"Изображение {image.PixelWidth}×{image.PixelHeight}";
            return item;
        }

        return null;
    }

    private static string? SaveImage(BitmapSource image, string id)
    {
        try
        {
            Directory.CreateDirectory(HistoryStore.ImagesDir);
            var name = id + ".png";
            var path = Path.Combine(HistoryStore.ImagesDir, name);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var fs = File.Create(path);
            encoder.Save(fs);
            return name;
        }
        catch (Exception ex)
        {
            Log.Error($"Не удалось сохранить изображение: {ex.Message}");
            return null;
        }
    }

    private bool IsMarkedSecret()
    {
        // Менеджеры паролей помечают буфер этими форматами, чтобы его не запоминали.
        if (Native.IsClipboardFormatAvailable(_fmtViewerIgnore)) return true;
        if (Native.IsClipboardFormatAvailable(_fmtExcludeMonitor)) return true;
        if (Native.IsClipboardFormatAvailable(_fmtCanIncludeHistory))
        {
            try
            {
                // Формат присутствует со значением 0 => включать в историю нельзя.
                var data = Clipboard.GetDataObject()?.GetData("CanIncludeInClipboardHistory");
                if (data is int flag && flag == 0) return true;
                if (data is byte[] bytes && bytes.Length > 0 && bytes[0] == 0) return true;
            }
            catch
            {
                // Не смогли прочитать — считаем, что ограничений нет.
            }
        }
        return false;
    }

    private bool IsIgnoredProcess(string processName) =>
        !string.IsNullOrEmpty(processName) &&
        _settings.IgnoredProcesses.Any(p =>
            processName.Contains(p, StringComparison.OrdinalIgnoreCase));

    private static (string process, IntPtr hwnd) GetClipboardOwnerApp()
    {
        try
        {
            var hwnd = Native.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return (string.Empty, IntPtr.Zero);
            Native.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0) return (string.Empty, hwnd);
            using var proc = Process.GetProcessById((int)pid);
            return (proc.ProcessName, hwnd);
        }
        catch
        {
            return (string.Empty, IntPtr.Zero);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pollTimer.Stop();
        Native.RemoveClipboardFormatListener(_hwnd);
    }
}
