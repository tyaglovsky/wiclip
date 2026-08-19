using System.ComponentModel;
using System.IO;
using System.Text.Json.Serialization;

namespace WiClip;

/// <summary>Папка библиотеки. Уровень один — вложенных папок нет.</summary>
public sealed class LibraryFolder : INotifyPropertyChanged
{
    /// <summary>Пустой Id — псевдопапка «Все», она не сохраняется на диск.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; OnChanged(nameof(Name)); } }
    }

    [JsonIgnore]
    public bool IsAll => Id.Length == 0;

    public static LibraryFolder All() => new() { Id = string.Empty, Name = Strings.FolderAll };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Запись библиотеки — то, что пользователь сохранил сам. В отличие от истории
/// не вытесняется и живёт, пока её не удалят.
/// </summary>
public sealed class LibraryItem : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FolderId { get; set; } = string.Empty;
    public ClipKind Kind { get; set; } = ClipKind.Text;

    private string _title = string.Empty;
    /// <summary>Название записи. Пустое — подставляется начало текста или имя файла.</summary>
    public string Title
    {
        get => _title;
        set { if (_title != value) { _title = value; OnChanged(nameof(Title)); OnChanged(nameof(Display)); } }
    }

    private string _text = string.Empty;
    public string Text
    {
        get => _text;
        set { if (_text != value) { _text = value; OnChanged(nameof(Text)); OnChanged(nameof(Display)); OnChanged(nameof(Meta)); } }
    }

    /// <summary>Имена файлов внутри папки записи (library\{Id}\...).</summary>
    public List<string> Files { get; set; } = new();

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Папка, куда скопированы файлы этой записи.</summary>
    [JsonIgnore]
    public string StorageDir => Path.Combine(LibraryStore.FilesDir, Id);

    /// <summary>Полные пути скопированных файлов.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> FilePaths =>
        Files.Select(f => Path.Combine(StorageDir, f)).ToArray();

    /// <summary>Что показывать в списке.</summary>
    [JsonIgnore]
    public string Display
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Title)) return Title;

            if (Kind != ClipKind.Text && Files.Count > 0) return Files[0];

            var text = Text.Trim();
            var nl = text.IndexOfAny(new[] { '\r', '\n' });
            var line = nl >= 0 ? text[..nl] : text;
            return line.Length > 120 ? line[..120] + "…" : line;
        }
    }

    [JsonIgnore]
    public string Meta
    {
        get
        {
            var kind = Kind switch
            {
                ClipKind.Image => Strings.MetaImage,
                ClipKind.Files => Files.Count == 1 ? Strings.MetaFile : Strings.Format("MetaFiles", Files.Count),
                _ => Strings.MetaText
            };

            var size = Kind == ClipKind.Text
                ? " · " + Strings.Format("MetaChars", Text.Length)
                : " · " + FormatSize(TotalBytes);

            return kind + size;
        }
    }

    [JsonIgnore]
    public long TotalBytes
    {
        get
        {
            long total = 0;
            foreach (var path in FilePaths)
            {
                try
                {
                    var info = new FileInfo(path);
                    if (info.Exists) total += info.Length;
                }
                catch
                {
                    // Недоступный файл просто не учитываем в размере.
                }
            }
            return total;
        }
    }

    /// <summary>Все ли файлы записи на месте.</summary>
    [JsonIgnore]
    public bool FilesPresent =>
        Kind == ClipKind.Text || (Files.Count > 0 && FilePaths.All(File.Exists));

    public ClipboardPayload ToPayload() => Kind switch
    {
        ClipKind.Image => new ClipboardPayload(Kind, Text, FilePaths.FirstOrDefault()),
        ClipKind.Files => new ClipboardPayload(Kind, Text, Files: FilePaths),
        _ => new ClipboardPayload(Kind, Text)
    };

    public bool Matches(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        foreach (var part in query.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var hit = Title.Contains(part, StringComparison.CurrentCultureIgnoreCase) ||
                      Text.Contains(part, StringComparison.CurrentCultureIgnoreCase) ||
                      Files.Any(f => f.Contains(part, StringComparison.CurrentCultureIgnoreCase));
            if (!hit) return false;
        }
        return true;
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / (1024.0 * 1024.0):0.#} MB"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public void Refresh()
    {
        OnChanged(nameof(Display));
        OnChanged(nameof(Meta));
    }
}
