using System.Windows.Input;

namespace WiClip;

/// <summary>Глобальная горячая клавиша через RegisterHotKey.</summary>
public sealed class HotKeyManager : IDisposable
{
    private const int HotKeyId = 0xB0B0 & 0xBFFF;

    private readonly IntPtr _hwnd;
    private bool _registered;

    public event Action? Pressed;

    public HotKeyManager(IntPtr hwnd) => _hwnd = hwnd;

    /// <summary>Регистрирует сочетание вида "Ctrl+Shift+V". Возвращает текст ошибки или null.</summary>
    public string? Register(string hotKey)
    {
        Unregister();

        if (!TryParse(hotKey, out var mods, out var vk, out var error))
            return error;

        if (!Native.RegisterHotKey(_hwnd, HotKeyId, mods | Native.MOD_NOREPEAT, vk))
            return Strings.Format("ErrHotKeyTaken", hotKey);

        _registered = true;
        Log.Info($"Hotkey registered: {hotKey}");
        return null;
    }

    public bool HandleMessage(int msg, IntPtr wParam)
    {
        if (msg != Native.WM_HOTKEY || wParam.ToInt32() != HotKeyId) return false;
        Pressed?.Invoke();
        return true;
    }

    public void Unregister()
    {
        if (!_registered) return;
        Native.UnregisterHotKey(_hwnd, HotKeyId);
        _registered = false;
    }

    public static bool TryParse(string hotKey, out uint mods, out uint vk, out string? error)
    {
        mods = 0;
        vk = 0;
        error = null;

        if (string.IsNullOrWhiteSpace(hotKey))
        {
            error = Strings.ErrHotKeyNotSet;
            return false;
        }

        string? keyPart = null;
        foreach (var raw in hotKey.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            var part = raw.Trim();
            switch (part.ToLowerInvariant())
            {
                case "ctrl" or "control": mods |= Native.MOD_CONTROL; break;
                case "shift": mods |= Native.MOD_SHIFT; break;
                case "alt": mods |= Native.MOD_ALT; break;
                case "win" or "windows": mods |= Native.MOD_WIN; break;
                default: keyPart = part; break;
            }
        }

        if (keyPart is null)
        {
            error = Strings.ErrHotKeyNoKey;
            return false;
        }

        if (mods == 0)
        {
            error = Strings.ErrHotKeyNoModifier;
            return false;
        }

        var normalized = keyPart switch
        {
            "`" or "~" => "Oem3",
            "-" => "OemMinus",
            "=" => "OemPlus",
            "[" => "OemOpenBrackets",
            "]" => "OemCloseBrackets",
            ";" => "Oem1",
            "'" => "OemQuotes",
            "," => "OemComma",
            "." => "OemPeriod",
            "/" => "OemQuestion",
            "\\" => "OemBackslash",
            _ => keyPart
        };

        if (normalized.Length == 1 && char.IsDigit(normalized[0]))
            normalized = "D" + normalized;

        if (!Enum.TryParse<Key>(normalized, ignoreCase: true, out var key) || key == Key.None)
        {
            error = Strings.Format("ErrHotKeyUnknownKey", keyPart);
            return false;
        }

        vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0)
        {
            error = Strings.Format("ErrHotKeyUnsupported", keyPart);
            return false;
        }

        return true;
    }

    public void Dispose() => Unregister();
}
