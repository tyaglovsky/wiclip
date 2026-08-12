using System.Globalization;

namespace WiClip;

/// <summary>Выбор языка интерфейса. Строки берутся из Resources/Strings*.resx.</summary>
public static class Localization
{
    /// <summary>
    /// Применить язык из настроек. «Auto» оставляет системный язык Windows.
    /// Вызывать до создания окон: XAML читает строки один раз при загрузке.
    /// </summary>
    public static void Apply(string language)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(language) ||
                language.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                Log.Info($"UI language: system ({CultureInfo.CurrentUICulture.Name}).");
                return;
            }

            var culture = new CultureInfo(language);
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentUICulture = culture;
            Log.Info($"UI language: {culture.Name}.");
        }
        catch (Exception ex)
        {
            Log.Warn($"Unknown language '{language}', falling back to system: {ex.Message}");
        }
    }
}
