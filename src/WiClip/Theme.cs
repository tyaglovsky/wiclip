using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace WiClip;

/// <summary>Светлая/тёмная палитра. Ключи ресурсов используются в XAML через DynamicResource.</summary>
public static class Theme
{
    public static void Apply(string theme)
    {
        var dark = theme.ToLowerInvariant() switch
        {
            "dark" => true,
            "light" => false,
            _ => IsSystemDark()
        };

        var r = Application.Current.Resources;

        if (dark)
        {
            Set(r, "BgBrush", "#FF1F2023");
            Set(r, "SurfaceBrush", "#FF2A2C31");
            Set(r, "BorderBrush_", "#FF3A3D44");
            Set(r, "TextBrush", "#FFF2F3F5");
            Set(r, "TextDimBrush", "#FF9AA0A8");
            Set(r, "HoverBrush", "#FF34373D");
            Set(r, "SelectionBrush", "#FF2578DC");
            Set(r, "SelectionTextBrush", "#FFFFFFFF");
            Set(r, "AccentBrush", "#FF4C9AF0");
        }
        else
        {
            Set(r, "BgBrush", "#FFFAFAFB");
            Set(r, "SurfaceBrush", "#FFFFFFFF");
            Set(r, "BorderBrush_", "#FFD9DCE1");
            Set(r, "TextBrush", "#FF1B1D21");
            Set(r, "TextDimBrush", "#FF6B7280");
            Set(r, "HoverBrush", "#FFEDF1F7");
            Set(r, "SelectionBrush", "#FF2578DC");
            Set(r, "SelectionTextBrush", "#FFFFFFFF");
            Set(r, "AccentBrush", "#FF1B62BF");
        }
    }

    private static void Set(ResourceDictionary r, string key, string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        r[key] = brush;
    }

    private static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch
        {
            return false;
        }
    }
}
