using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WiClip;

public sealed class AppSettings
{
    /// <summary>Горячая клавиша в формате "Ctrl+Shift+V", "Alt+`", "Win+Shift+C" и т.п.</summary>
    public string HotKey { get; set; } = "Ctrl+Shift+V";

    /// <summary>Сколько записей хранить (закреплённые не считаются).</summary>
    public int MaxItems { get; set; } = 100;

    /// <summary>Сохранять историю на диск между запусками. False — только в памяти.</summary>
    public bool PersistHistory { get; set; } = true;

    /// <summary>Запоминать картинки из буфера, а не только текст.</summary>
    public bool CaptureImages { get; set; } = true;

    /// <summary>После выбора записи автоматически отправлять Ctrl+V в активное окно.</summary>
    public bool PasteOnSelect { get; set; } = true;

    /// <summary>Игнорировать буфер, помеченный менеджерами паролей как секретный.</summary>
    public bool RespectSecretClipboard { get; set; } = true;

    /// <summary>Auto | Light | Dark</summary>
    public string Theme { get; set; } = "Auto";

    /// <summary>Не запоминать содержимое, скопированное из этих процессов (без .exe).</summary>
    public List<string> IgnoredProcesses { get; set; } = new()
    {
        "keepass", "keepassxc", "1password", "bitwarden", "lastpass", "dashlane"
    };

    /// <summary>Максимальная длина сохраняемого текста, символов.</summary>
    public int MaxTextLength { get; set; } = 200_000;

    [JsonIgnore]
    public static string Dir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WiClip");

    [JsonIgnore]
    public static string Path_ { get; } = Path.Combine(Dir, "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>True, если файла настроек ещё не было — первый запуск после установки.</summary>
    [JsonIgnore]
    public static bool FirstRun { get; private set; }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(Path_))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path_)) ?? new AppSettings();

            FirstRun = true;
        }
        catch (Exception ex)
        {
            Log.Warn($"Не удалось прочитать settings.json: {ex.Message}");
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Path_, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch (Exception ex)
        {
            Log.Warn($"Не удалось сохранить settings.json: {ex.Message}");
        }
    }
}
