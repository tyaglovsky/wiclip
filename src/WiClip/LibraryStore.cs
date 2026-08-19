using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WiClip;

/// <summary>
/// Библиотека: записи, которые пользователь сохранил сам. Хранится в
/// %APPDATA%\WiClip\library.json, файлы копируются в %APPDATA%\WiClip\library\{id}\.
/// </summary>
public sealed class LibraryStore
{
    /// <summary>Файлы больше этого размера в библиотеку не копируем.</summary>
    public const long MaxFileBytes = 100L * 1024 * 1024;

    public static string FilesDir { get; } = Path.Combine(AppSettings.Dir, "library");
    private static string IndexFile => Path.Combine(AppSettings.Dir, "library.json");

    private readonly object _saveGate = new();

    /// <summary>Первая папка — псевдопапка «Все», на диск не сохраняется.</summary>
    public ObservableCollection<LibraryFolder> Folders { get; } = new();

    public ObservableCollection<LibraryItem> Items { get; } = new();

    public LibraryStore()
    {
        Folders.Add(LibraryFolder.All());
        Load();
    }

    // ------------------------------------------------------------- записи ---

    public LibraryItem AddText(string text, string title, string folderId)
    {
        var item = new LibraryItem
        {
            Kind = ClipKind.Text,
            Text = text,
            Title = title,
            FolderId = folderId
        };

        Items.Insert(0, item);
        Save();
        return item;
    }

    /// <summary>
    /// Скопировать файлы в библиотеку одной записью. Возвращает запись или null,
    /// если ни один файл скопировать не удалось; ошибки складываются в errors.
    /// </summary>
    public LibraryItem? AddFiles(IEnumerable<string> paths, string folderId, IList<string> errors)
    {
        var item = new LibraryItem { Kind = ClipKind.Files, FolderId = folderId };
        var copied = new List<string>();

        foreach (var source in paths)
        {
            try
            {
                var info = new FileInfo(source);
                if (!info.Exists) continue;

                if (info.Length > MaxFileBytes)
                {
                    errors.Add(Strings.Format("ErrFileTooBig", info.Name, MaxFileBytes / (1024 * 1024)));
                    continue;
                }

                Directory.CreateDirectory(item.StorageDir);
                var name = UniqueName(item.StorageDir, info.Name);
                File.Copy(source, Path.Combine(item.StorageDir, name));
                copied.Add(name);
            }
            catch (Exception ex)
            {
                errors.Add(Strings.Format("ErrCopyFile", Path.GetFileName(source), ex.Message));
                Log.Warn($"Could not copy '{source}' into the library: {ex.Message}");
            }
        }

        if (copied.Count == 0)
        {
            TryDeleteDirectory(item.StorageDir);
            return null;
        }

        item.Files = copied;
        item.Text = string.Join("\n", item.FilePaths);
        Items.Insert(0, item);
        Save();
        return item;
    }

    /// <summary>Сохранить запись истории в библиотеку (текст, картинку или файлы).</summary>
    public LibraryItem? AddFromHistory(ClipItem source, string folderId, IList<string> errors)
    {
        switch (source.Kind)
        {
            case ClipKind.Text:
                return AddText(source.Text, string.Empty, folderId);

            case ClipKind.Image:
                if (string.IsNullOrEmpty(source.ImagePath)) return null;
                var image = AddFiles(new[] { source.ImagePath }, folderId, errors);
                if (image is not null)
                {
                    // Картинка должна вставляться как изображение, а не как файл.
                    image.Kind = ClipKind.Image;
                    image.Text = source.Text;
                    Save();
                }
                return image;

            case ClipKind.Files:
                var files = source.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                       .Select(f => f.Trim())
                                       .Where(f => f.Length > 0);
                return AddFiles(files, folderId, errors);

            default:
                return null;
        }
    }

    public void Remove(LibraryItem item)
    {
        Items.Remove(item);
        TryDeleteDirectory(item.StorageDir);
        Save();
    }

    // ------------------------------------------------------------- папки ---

    public LibraryFolder AddFolder(string name)
    {
        var folder = new LibraryFolder { Name = name };
        Folders.Add(folder);
        Save();
        return folder;
    }

    public void RenameFolder(LibraryFolder folder, string name)
    {
        if (folder.IsAll) return;
        folder.Name = name;
        Save();
    }

    /// <summary>Удалить папку вместе с её записями.</summary>
    public void RemoveFolder(LibraryFolder folder)
    {
        if (folder.IsAll) return;

        foreach (var item in Items.Where(i => i.FolderId == folder.Id).ToList())
            Remove(item);

        Folders.Remove(folder);
        Save();
    }

    // ---------------------------------------------------------- хранение ---

    private sealed class Snapshot
    {
        [JsonPropertyName("folders")] public List<LibraryFolder> Folders { get; set; } = new();
        [JsonPropertyName("items")] public List<LibraryItem> Items { get; set; } = new();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(IndexFile)) return;

            var data = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(IndexFile));
            if (data is null) return;

            foreach (var folder in data.Folders.Where(f => !f.IsAll))
                Folders.Add(folder);

            foreach (var item in data.Items)
                Items.Add(item);

            Log.Info($"Library loaded: {Items.Count} entries in {Folders.Count - 1} folders.");
        }
        catch (Exception ex)
        {
            Log.Error($"Could not load the library: {ex.Message}");
        }
    }

    public void Save()
    {
        var snapshot = new Snapshot
        {
            Folders = Folders.Where(f => !f.IsAll).ToList(),
            Items = Items.ToList()
        };

        Task.Run(() =>
        {
            lock (_saveGate)
            {
                try
                {
                    Directory.CreateDirectory(AppSettings.Dir);
                    var tmp = IndexFile + ".tmp";
                    File.WriteAllText(tmp, JsonSerializer.Serialize(snapshot));
                    File.Move(tmp, IndexFile, overwrite: true);
                }
                catch (Exception ex)
                {
                    Log.Error($"Could not save the library: {ex.Message}");
                }
            }
        });
    }

    private static string UniqueName(string directory, string name)
    {
        var candidate = name;
        var stem = Path.GetFileNameWithoutExtension(name);
        var extension = Path.GetExtension(name);

        for (var i = 2; File.Exists(Path.Combine(directory, candidate)); i++)
            candidate = $"{stem} ({i}){extension}";

        return candidate;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not delete '{path}': {ex.Message}");
        }
    }
}
