using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace WiClip;

/// <summary>История буфера обмена: список в памяти + сохранение в %APPDATA%\WiClip.</summary>
public sealed class HistoryStore
{
    public static string ImagesDir { get; } = Path.Combine(AppSettings.Dir, "images");
    private static string HistoryFile => Path.Combine(AppSettings.Dir, "history.json");

    private readonly AppSettings _settings;
    private readonly object _saveGate = new();

    public ObservableCollection<ClipItem> Items { get; } = new();

    public HistoryStore(AppSettings settings)
    {
        _settings = settings;
        Load();
    }

    public void Add(ClipItem item)
    {
        // Дубликаты не плодим — одинаковую запись просто поднимаем наверх.
        var dup = Items.FirstOrDefault(i =>
            i.Kind == item.Kind &&
            i.Kind != ClipKind.Image &&
            string.Equals(i.Text, item.Text, StringComparison.Ordinal));

        if (dup is not null)
        {
            dup.CreatedUtc = DateTime.UtcNow;
            dup.RefreshMeta();
            var idx = Items.IndexOf(dup);
            if (idx > 0) Items.Move(idx, 0);
            Save();
            return;
        }

        Items.Insert(0, item);
        Trim();
        Save();
    }

    public void Remove(ClipItem item)
    {
        Items.Remove(item);
        DeleteImageFile(item);
        Save();
    }

    /// <summary>Очистить всё, кроме закреплённых записей.</summary>
    public void Clear(bool includePinned = false)
    {
        foreach (var item in Items.ToList())
        {
            if (!includePinned && item.Pinned) continue;
            Items.Remove(item);
            DeleteImageFile(item);
        }
        Save();
    }

    public void TogglePin(ClipItem item)
    {
        item.Pinned = !item.Pinned;
        // Закреплённые всегда наверху списка.
        Reorder();
        Save();
    }

    private void Reorder()
    {
        var sorted = Items
            .OrderByDescending(i => i.Pinned)
            .ThenByDescending(i => i.CreatedUtc)
            .ToList();

        for (var i = 0; i < sorted.Count; i++)
        {
            var cur = Items.IndexOf(sorted[i]);
            if (cur != i) Items.Move(cur, i);
        }
    }

    private void Trim()
    {
        var unpinned = Items.Where(i => !i.Pinned).ToList();
        for (var i = _settings.MaxItems; i < unpinned.Count; i++)
        {
            Items.Remove(unpinned[i]);
            DeleteImageFile(unpinned[i]);
        }
    }

    private static void DeleteImageFile(ClipItem item)
    {
        try
        {
            if (item.Kind == ClipKind.Image && File.Exists(item.ImagePath))
                File.Delete(item.ImagePath);
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not delete {item.ImageFile}: {ex.Message}");
        }
    }

    private void Load()
    {
        if (!_settings.PersistHistory) return;
        try
        {
            if (!File.Exists(HistoryFile)) return;
            var loaded = JsonSerializer.Deserialize<List<ClipItem>>(File.ReadAllText(HistoryFile));
            if (loaded is null) return;

            foreach (var item in loaded.OrderByDescending(i => i.Pinned).ThenByDescending(i => i.CreatedUtc))
            {
                // Пропускаем записи, у которых потерялся файл картинки.
                if (item.Kind == ClipKind.Image && !File.Exists(item.ImagePath)) continue;
                Items.Add(item);
            }
            Log.Info($"History loaded: {Items.Count} entries.");
        }
        catch (Exception ex)
        {
            Log.Error($"Could not load history: {ex.Message}");
        }
    }

    public void Save()
    {
        if (!_settings.PersistHistory) return;
        var snapshot = Items.ToList();
        Task.Run(() =>
        {
            lock (_saveGate)
            {
                try
                {
                    Directory.CreateDirectory(AppSettings.Dir);
                    var tmp = HistoryFile + ".tmp";
                    File.WriteAllText(tmp, JsonSerializer.Serialize(snapshot));
                    File.Move(tmp, HistoryFile, overwrite: true);
                }
                catch (Exception ex)
                {
                    Log.Error($"Could not save history: {ex.Message}");
                }
            }
        });
    }

    /// <summary>Удалить историю и картинки с диска (например, при выключении сохранения).</summary>
    public static void PurgeDisk()
    {
        try
        {
            if (File.Exists(HistoryFile)) File.Delete(HistoryFile);
            if (Directory.Exists(ImagesDir)) Directory.Delete(ImagesDir, recursive: true);
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not purge on-disk data: {ex.Message}");
        }
    }
}
