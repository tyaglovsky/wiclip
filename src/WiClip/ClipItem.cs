using System.ComponentModel;
using System.IO;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;

namespace WiClip;

public enum ClipKind { Text, Image, Files }

public sealed class ClipItem : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ClipKind Kind { get; set; } = ClipKind.Text;

    /// <summary>Полный текст (для Kind = Text) или список путей через перевод строки (Files).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Имя PNG-файла в подпапке images (только для Kind = Image).</summary>
    public string? ImageFile { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string SourceApp { get; set; } = string.Empty;

    private bool _pinned;
    public bool Pinned
    {
        get => _pinned;
        set { if (_pinned != value) { _pinned = value; OnChanged(nameof(Pinned)); } }
    }

    // ---- вычисляемые свойства для UI ----

    [JsonIgnore]
    public string Preview
    {
        get
        {
            switch (Kind)
            {
                case ClipKind.Image:
                    return Strings.PreviewImage;
                case ClipKind.Files:
                    var files = Text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    return files.Length == 1
                        ? "\U0001F4C4  " + Path.GetFileName(files[0].Trim())
                        : Strings.Format("PreviewFilesMany", files.Length);
                default:
                    var t = Text.Trim();
                    var nl = t.IndexOfAny(new[] { '\r', '\n' });
                    var line = nl >= 0 ? t[..nl] : t;
                    if (line.Length > 200) line = line[..200] + "…";
                    var extraLines = t.Count(c => c == '\n');
                    return extraLines > 0 ? $"{line}  ⏎+{extraLines}" : line;
            }
        }
    }

    [JsonIgnore]
    public string Meta
    {
        get
        {
            var when = Humanize(DateTime.UtcNow - CreatedUtc);
            var size = Kind == ClipKind.Text
                ? " · " + Strings.Format("MetaChars", Text.Length)
                : string.Empty;
            var app = string.IsNullOrEmpty(SourceApp) ? string.Empty : $" · {SourceApp}";
            return when + size + app;
        }
    }

    [JsonIgnore]
    public string ImagePath =>
        ImageFile is null ? string.Empty : Path.Combine(HistoryStore.ImagesDir, ImageFile);

    private BitmapImage? _thumb;
    [JsonIgnore]
    public BitmapImage? Thumbnail
    {
        get
        {
            if (Kind != ClipKind.Image) return null;
            if (_thumb is not null) return _thumb;
            try
            {
                if (!File.Exists(ImagePath)) return null;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelHeight = 96;
                bmp.UriSource = new Uri(ImagePath);
                bmp.EndInit();
                bmp.Freeze();
                _thumb = bmp;
            }
            catch (Exception ex)
            {
                Log.Warn($"Could not load thumbnail {ImageFile}: {ex.Message}");
            }
            return _thumb;
        }
    }

    /// <summary>Данные для буфера обмена: у истории картинки лежат в своей папке.</summary>
    public ClipboardPayload ToPayload() => Kind switch
    {
        ClipKind.Image => new ClipboardPayload(Kind, Text, ImagePath),
        ClipKind.Files => new ClipboardPayload(Kind, Text,
            Files: Text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                       .Select(f => f.Trim())
                       .Where(f => f.Length > 0)
                       .ToArray()),
        _ => new ClipboardPayload(Kind, Text)
    };

    public bool Matches(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        foreach (var part in query.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var inText = Text.Contains(part, StringComparison.CurrentCultureIgnoreCase);
            var inApp = SourceApp.Contains(part, StringComparison.CurrentCultureIgnoreCase);
            if (!inText && !inApp) return false;
        }
        return true;
    }

    private static string Humanize(TimeSpan span)
    {
        if (span.TotalSeconds < 60) return Strings.TimeJustNow;
        if (span.TotalMinutes < 60) return Strings.Format("TimeMinutes", (int)span.TotalMinutes);
        if (span.TotalHours < 24) return Strings.Format("TimeHours", (int)span.TotalHours);
        return Strings.Format("TimeDays", (int)span.TotalDays);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public void RefreshMeta() => OnChanged(nameof(Meta));
}
