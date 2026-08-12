using Microsoft.Win32;

namespace WiClip;

/// <summary>Автозапуск для текущего пользователя (HKCU\...\Run).</summary>
public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WiClip";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(ValueName) is not null;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>Автозапуск прописан установщиком для всех пользователей машины (HKLM).</summary>
    public static bool IsMachineWide
    {
        get
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(RunKey);
                return key?.GetValue(ValueName) is not null;
            }
            catch
            {
                return false;
            }
        }
    }

    public static void Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key is null) return;

            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (exe is null) return;
                key.SetValue(ValueName, $"\"{exe}\" --autostart");
            }
            else if (key.GetValue(ValueName) is not null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Не удалось изменить автозапуск: {ex.Message}");
        }
    }
}
