using System.Globalization;
using System.Resources;

namespace WiClip;

/// <summary>
/// Localised UI strings. Generated from Resources/Strings.resx - do not edit by hand,
/// see tools note in README. English is the neutral language, ru is a satellite assembly.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager =
        new("WiClip.Resources.Strings", typeof(Strings).Assembly);

    private static bool _resourcesBroken;

    /// <summary>
    /// Looks a string up in the current UI culture. Never throws: a missing or broken
    /// resource must not take the whole application down, the key is shown instead.
    /// </summary>
    public static string Get(string key)
    {
        if (_resourcesBroken) return key;
        try
        {
            return Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
        }
        catch (Exception ex)
        {
            _resourcesBroken = true;
            Log.Error($"String resources are unavailable ({ex.GetType().Name}: {ex.Message}). " +
                      "Falling back to resource keys.");
            return key;
        }
    }

    /// <summary>Same, but formatted with the current UI culture.</summary>
    public static string Format(string key, params object[] args)
    {
        try
        {
            return string.Format(CultureInfo.CurrentUICulture, Get(key), args);
        }
        catch (FormatException ex)
        {
            Log.Warn($"Bad format string for '{key}': {ex.Message}");
            return Get(key);
        }
    }

    /// <summary>WiClip - clipboard history ({0})</summary>
    public static string TrayTooltip => Get("TrayTooltip");

    /// <summary>Open history {0}</summary>
    public static string MenuOpen => Get("MenuOpen");

    /// <summary>Clear history</summary>
    public static string MenuClear => Get("MenuClear");

    /// <summary>Settings...</summary>
    public static string MenuSettings => Get("MenuSettings");

    /// <summary>Exit</summary>
    public static string MenuExit => Get("MenuExit");

    /// <summary>Delete the whole clipboard history? Pinned entries are kept.</summary>
    public static string ClearConfirm => Get("ClearConfirm");

    /// <summary>Clipboard history is running. Press {0} to open it.</summary>
    public static string BalloonStarted => Get("BalloonStarted");

    /// <summary>Clipboard</summary>
    public static string WindowTitle => Get("WindowTitle");

    /// <summary>Search history...</summary>
    public static string SearchPlaceholder => Get("SearchPlaceholder");

    /// <summary>Settings</summary>
    public static string TooltipSettings => Get("TooltipSettings");

    /// <summary>Close (Esc)</summary>
    public static string TooltipClose => Get("TooltipClose");

    /// <summary>Nothing here yet. Copy something and it will show up.</summary>
    public static string EmptyHistory => Get("EmptyHistory");

    /// <summary>Nothing found.</summary>
    public static string EmptySearch => Get("EmptySearch");

    /// <summary>Click - copy · Double click or Enter - paste · Alt+1...9 - quick pick · Ctrl+P - pin · Shift+Delete - remove · Esc - close</summary>
    public static string Hints => Get("Hints");

    /// <summary>✓ Copied to clipboard</summary>
    public static string ToastCopied => Get("ToastCopied");

    /// <summary>Could not copy - the clipboard is busy</summary>
    public static string ToastCopyFailed => Get("ToastCopyFailed");

    /// <summary>🖼  Image</summary>
    public static string PreviewImage => Get("PreviewImage");

    /// <summary>📄  Files: {0}</summary>
    public static string PreviewFilesMany => Get("PreviewFilesMany");

    /// <summary>Image {0}×{1}</summary>
    public static string ImageSize => Get("ImageSize");

    /// <summary>{0} chars</summary>
    public static string MetaChars => Get("MetaChars");

    /// <summary>just now</summary>
    public static string TimeJustNow => Get("TimeJustNow");

    /// <summary>{0} min ago</summary>
    public static string TimeMinutes => Get("TimeMinutes");

    /// <summary>{0} h ago</summary>
    public static string TimeHours => Get("TimeHours");

    /// <summary>{0} d ago</summary>
    public static string TimeDays => Get("TimeDays");

    /// <summary>WiClip settings</summary>
    public static string SettingsTitle => Get("SettingsTitle");

    /// <summary>Hotkey</summary>
    public static string LabelHotKey => Get("LabelHotKey");

    /// <summary>For example: Ctrl+Shift+V, Alt+`, Win+Shift+C</summary>
    public static string HintHotKey => Get("HintHotKey");

    /// <summary>How many entries to keep</summary>
    public static string LabelMaxItems => Get("LabelMaxItems");

    /// <summary>Appearance</summary>
    public static string LabelTheme => Get("LabelTheme");

    /// <summary>Match system</summary>
    public static string ThemeAuto => Get("ThemeAuto");

    /// <summary>Light</summary>
    public static string ThemeLight => Get("ThemeLight");

    /// <summary>Dark</summary>
    public static string ThemeDark => Get("ThemeDark");

    /// <summary>Language</summary>
    public static string LabelLanguage => Get("LabelLanguage");

    /// <summary>Match system</summary>
    public static string LanguageAuto => Get("LanguageAuto");

    /// <summary>Start with Windows</summary>
    public static string CheckAutostart => Get("CheckAutostart");

    /// <summary>Start with Windows (set by the installer for all users)</summary>
    public static string CheckAutostartByInstaller => Get("CheckAutostartByInstaller");

    /// <summary>Keep history between sessions</summary>
    public static string CheckPersist => Get("CheckPersist");

    /// <summary>Remember images</summary>
    public static string CheckImages => Get("CheckImages");

    /// <summary>Paste the picked entry into the active window</summary>
    public static string CheckPaste => Get("CheckPaste");

    /// <summary>Ignore clipboard from password managers</summary>
    public static string CheckSecret => Get("CheckSecret");

    /// <summary>Do not record copies from these processes (comma separated)</summary>
    public static string LabelIgnored => Get("LabelIgnored");

    /// <summary>Data folder</summary>
    public static string ButtonDataFolder => Get("ButtonDataFolder");

    /// <summary>Cancel</summary>
    public static string ButtonCancel => Get("ButtonCancel");

    /// <summary>Save</summary>
    public static string ButtonSave => Get("ButtonSave");

    /// <summary>The language applies to windows opened from now on.</summary>
    public static string LanguageRestartNote => Get("LanguageRestartNote");

    /// <summary>No shortcut specified.</summary>
    public static string ErrHotKeyNotSet => Get("ErrHotKeyNotSet");

    /// <summary>The shortcut has no main key.</summary>
    public static string ErrHotKeyNoKey => Get("ErrHotKeyNoKey");

    /// <summary>At least one modifier is required (Ctrl, Alt, Shift or Win).</summary>
    public static string ErrHotKeyNoModifier => Get("ErrHotKeyNoModifier");

    /// <summary>Could not recognise the key "{0}".</summary>
    public static string ErrHotKeyUnknownKey => Get("ErrHotKeyUnknownKey");

    /// <summary>The key "{0}" is not supported.</summary>
    public static string ErrHotKeyUnsupported => Get("ErrHotKeyUnsupported");

    /// <summary>The shortcut "{0}" is already taken by another program. Pick a different one in WiClip settings.</summary>
    public static string ErrHotKeyTaken => Get("ErrHotKeyTaken");

    /// <summary>Invalid shortcut.</summary>
    public static string ErrHotKeyInvalid => Get("ErrHotKeyInvalid");

    /// <summary>The number of entries must be between 1 and 10000.</summary>
    public static string ErrMaxItems => Get("ErrMaxItems");
}
