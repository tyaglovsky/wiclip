using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WiClip;

public partial class HistoryWindow : Window
{
    private readonly HistoryStore _store;
    private readonly AppSettings _settings;
    private readonly ClipboardMonitor _monitor;
    private readonly ICollectionView _view;

    private readonly DispatcherTimer _toastTimer;

    private IntPtr _target;
    private bool _suppressHideOnDeactivate;
    private bool _reallyClose;

    public HistoryWindow(HistoryStore store, AppSettings settings, ClipboardMonitor monitor)
    {
        _store = store;
        _settings = settings;
        _monitor = monitor;

        InitializeComponent();

        _view = CollectionViewSource.GetDefaultView(_store.Items);
        _view.Filter = o => o is ClipItem item && item.Matches(SearchBox.Text);
        List.ItemsSource = _view;

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            ToastText.Visibility = Visibility.Collapsed;
            HintText.Visibility = Visibility.Visible;
        };

        Deactivated += (_, _) => { if (!_suppressHideOnDeactivate) HideWindow(); };
        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>Показать окно у курсора; target — окно, куда потом вставлять.</summary>
    public void ShowFor(IntPtr target)
    {
        _target = target;

        SearchBox.Text = string.Empty;
        foreach (var item in _store.Items) item.RefreshMeta();
        _view.Refresh();

        Opacity = 0;
        Show();
        PositionNearCursor();
        Opacity = 1;

        Activate();
        SearchBox.Focus();

        if (List.Items.Count > 0) List.SelectedIndex = 0;
        UpdateEmptyState();

        Log.Info($"Окно истории открыто: записей в истории {_store.Items.Count}, " +
                 $"показано {List.Items.Count}.");
    }

    private void PositionNearCursor()
    {
        try
        {
            if (!Native.GetCursorPos(out var pt)) return;

            var screen = System.Windows.Forms.Screen.FromPoint(
                new System.Drawing.Point(pt.X, pt.Y));
            var area = screen.WorkingArea;

            // Физические пиксели -> DIP: у мониторов может быть разный масштаб.
            var dpi = VisualTreeHelper.GetDpi(this);
            var scaleX = dpi.DpiScaleX == 0 ? 1 : dpi.DpiScaleX;
            var scaleY = dpi.DpiScaleY == 0 ? 1 : dpi.DpiScaleY;

            var left = pt.X / scaleX + 8;
            var top = pt.Y / scaleY + 8;

            var areaLeft = area.Left / scaleX;
            var areaTop = area.Top / scaleY;
            var areaRight = area.Right / scaleX;
            var areaBottom = area.Bottom / scaleY;

            if (left + Width > areaRight) left = areaRight - Width;
            if (top + Height > areaBottom) top = areaBottom - Height;
            if (left < areaLeft) left = areaLeft;
            if (top < areaTop) top = areaTop;

            Left = left;
            Top = top;
        }
        catch (Exception ex)
        {
            Log.Warn($"Не удалось позиционировать окно: {ex.Message}");
        }
    }

    private void HideWindow()
    {
        Hide();
        SearchBox.Text = string.Empty;
    }

    private void UpdateEmptyState()
    {
        EmptyHint.Visibility = List.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyHint.Text = _store.Items.Count == 0
            ? "Пока пусто. Скопируйте что-нибудь — запись появится здесь."
            : "Ничего не найдено.";
        SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _view.Refresh();
        if (List.Items.Count > 0) List.SelectedIndex = 0;
        UpdateEmptyState();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        var alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;

        switch (e.Key)
        {
            case Key.Escape:
                HideWindow();
                e.Handled = true;
                return;

            case Key.Enter:
                if (List.SelectedItem is ClipItem chosen)
                    Use(chosen, paste: !ctrl);
                e.Handled = true;
                return;

            case Key.Down:
                Move(+1);
                e.Handled = true;
                return;

            case Key.Up:
                Move(-1);
                e.Handled = true;
                return;

            case Key.PageDown:
                Move(+8);
                e.Handled = true;
                return;

            case Key.PageUp:
                Move(-8);
                e.Handled = true;
                return;

            case Key.Delete when shift:
                if (List.SelectedItem is ClipItem toDelete)
                {
                    var index = List.SelectedIndex;
                    _store.Remove(toDelete);
                    _view.Refresh();
                    if (List.Items.Count > 0)
                        List.SelectedIndex = Math.Min(index, List.Items.Count - 1);
                    UpdateEmptyState();
                }
                e.Handled = true;
                return;

            case Key.P when ctrl:
                if (List.SelectedItem is ClipItem toPin)
                {
                    _store.TogglePin(toPin);
                    _view.Refresh();
                    List.SelectedItem = toPin;
                    List.ScrollIntoView(toPin);
                }
                e.Handled = true;
                return;
        }

        // Alt+1…9 — быстрый выбор
        if (alt && e.SystemKey >= Key.D1 && e.SystemKey <= Key.D9)
        {
            var index = e.SystemKey - Key.D1;
            if (index < List.Items.Count && List.Items[index] is ClipItem quick)
                Use(quick, paste: true);
            e.Handled = true;
        }
    }

    private void Move(int delta)
    {
        if (List.Items.Count == 0) return;
        var index = Math.Clamp(List.SelectedIndex + delta, 0, List.Items.Count - 1);
        List.SelectedIndex = index;
        List.ScrollIntoView(List.Items[index]);
    }

    /// <summary>Скопировать запись в буфер и, если нужно, вставить в целевое окно.</summary>
    private void Use(ClipItem item, bool paste)
    {
        HideWindow();

        // Собственную запись в буфер в историю не пишем.
        _monitor.SuppressNext();

        if (!Paster.CopyToClipboard(item)) return;
        if (paste && _settings.PasteOnSelect) Paster.PasteInto(_target);
    }

    /// <summary>Одиночный клик — просто скопировать запись, окно остаётся открытым.</summary>
    private void List_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        if (ItemsControl.ContainerFromElement(List, source) is not ListBoxItem container) return;
        if (container.DataContext is not ClipItem item) return;

        List.SelectedItem = item;
        CopyOnly(item);
    }

    private void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (List.SelectedItem is ClipItem item) Use(item, paste: true);
    }

    /// <summary>Скопировать в буфер без вставки и показать подтверждение.</summary>
    private void CopyOnly(ClipItem item)
    {
        _monitor.SuppressNext();

        if (Paster.CopyToClipboard(item))
            ShowToast(item.Kind == ClipKind.Text ? "✓ Скопировано в буфер" : "✓ Скопировано");
        else
            ShowToast("Не удалось скопировать — буфер занят");
    }

    private void ShowToast(string text)
    {
        ToastText.Text = text;
        ToastText.Visibility = Visibility.Visible;
        HintText.Visibility = Visibility.Hidden;

        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => HideWindow();

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _suppressHideOnDeactivate = true;
        HideWindow();
        _suppressHideOnDeactivate = false;

        var dlg = new SettingsWindow(_settings);
        if (dlg.ShowDialog() == true)
        {
            _settings.Save();
            Theme.Apply(_settings.Theme);
            SettingsApplied?.Invoke();
        }
    }

    /// <summary>Вызывается после изменения настроек из окна истории.</summary>
    public event Action? SettingsApplied;

    protected override void OnClosing(CancelEventArgs e)
    {
        // Крестик и Alt+F4 прячут окно, приложение продолжает жить в трее.
        if (!_reallyClose)
        {
            e.Cancel = true;
            HideWindow();
            return;
        }
        base.OnClosing(e);
    }

    public void CloseForReal()
    {
        _reallyClose = true;
        Close();
    }
}
