using System.IO;
using System.Text;

namespace WiClip;

/// <summary>Простой лог в %APPDATA%\WiClip\wiclip.log с обрезкой по размеру.</summary>
internal static class Log
{
    private static readonly object Gate = new();
    private static readonly string File_ = Path.Combine(AppSettings.Dir, "wiclip.log");
    private const long MaxBytes = 512 * 1024;

    // С BOM, иначе PowerShell и «Блокнот» читают лог как cp1251 и показывают кракозябры.
    private static readonly Encoding Utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    public static void Info(string msg) => Write("INFO", msg);
    public static void Warn(string msg) => Write("WARN", msg);
    public static void Error(string msg) => Write("ERROR", msg);

    private static void Write(string level, string msg)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppSettings.Dir);
                var fi = new FileInfo(File_);
                if (fi.Exists && fi.Length > MaxBytes)
                    File.Move(File_, File_ + ".old", overwrite: true);

                File.AppendAllText(File_,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {msg}{Environment.NewLine}",
                    Utf8Bom);
            }
        }
        catch
        {
            // Логирование никогда не должно ронять приложение.
        }
    }
}
