using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace WiClip;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    public SettingsWindow(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();

        HotKeyBox.Text = settings.HotKey;
        MaxItemsBox.Text = settings.MaxItems.ToString();
        PersistBox.IsChecked = settings.PersistHistory;
        ImagesBox.IsChecked = settings.CaptureImages;
        PasteBox.IsChecked = settings.PasteOnSelect;
        SecretBox.IsChecked = settings.RespectSecretClipboard;
        if (Autostart.IsMachineWide)
        {
            // Запись сделана установщиком в HKLM — из настроек её не снять без прав админа.
            AutostartBox.IsChecked = true;
            AutostartBox.IsEnabled = false;
            AutostartBox.Content = "Запускать при входе в Windows (задано установщиком для всех)";
        }
        else
        {
            AutostartBox.IsChecked = Autostart.IsEnabled;
        }
        IgnoredBox.Text = string.Join(", ", settings.IgnoredProcesses);

        foreach (ComboBoxItem item in ThemeBox.Items)
        {
            if ((string)item.Tag == settings.Theme) ThemeBox.SelectedItem = item;
        }
        ThemeBox.SelectedItem ??= ThemeBox.Items[0];
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var hotKey = HotKeyBox.Text.Trim();
        if (!HotKeyManager.TryParse(hotKey, out _, out _, out var error))
        {
            ShowError(error ?? "Некорректное сочетание клавиш.");
            return;
        }

        if (!int.TryParse(MaxItemsBox.Text.Trim(), out var maxItems) || maxItems < 1 || maxItems > 10_000)
        {
            ShowError("Количество записей должно быть числом от 1 до 10000.");
            return;
        }

        _settings.HotKey = hotKey;
        _settings.MaxItems = maxItems;
        _settings.PersistHistory = PersistBox.IsChecked == true;
        _settings.CaptureImages = ImagesBox.IsChecked == true;
        _settings.PasteOnSelect = PasteBox.IsChecked == true;
        _settings.RespectSecretClipboard = SecretBox.IsChecked == true;
        _settings.Theme = (string)((ComboBoxItem)ThemeBox.SelectedItem).Tag;
        _settings.IgnoredProcesses = IgnoredBox.Text
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        if (!Autostart.IsMachineWide) Autostart.Set(AutostartBox.IsChecked == true);

        DialogResult = true;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void DataFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppSettings.Dir);
            Process.Start(new ProcessStartInfo("explorer.exe", AppSettings.Dir) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warn($"Не удалось открыть папку данных: {ex.Message}");
        }
    }
}
