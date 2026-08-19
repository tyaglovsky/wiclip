using System.ComponentModel;
using System.IO;
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
    private readonly LibraryStore _library;
    private readonly AppSettings _settings;
    private readonly ClipboardMonitor _monitor;

    private readonly ICollectionView _view;
    private readonly ICollectionView _libraryView;
    private readonly DispatcherTimer _toastTimer;

    private IntPtr _target;
    private bool _reallyClose;

    /// <summary>Пока открыт диалог, окно не должно прятаться по потере фокуса.</summary>
    private bool _modalOpen;

    public HistoryWindow(HistoryStore store, LibraryStore library,
                         AppSettings settings, ClipboardMonitor monitor)
    {
        _store = store;
        _library = library;
        _settings = settings;
        _monitor = monitor;

        InitializeComponent();

        _view = CollectionViewSource.GetDefaultView(_store.Items);
        _view.Filter = o => o is ClipItem item && item.Matches(SearchBox.Text);
        List.ItemsSource = _view;

        _libraryView = CollectionViewSource.GetDefaultView(_library.Items);
        _libraryView.Filter = o => o is LibraryItem item && InCurrentFolder(item) && item.Matches(SearchBox.Text);
        LibraryList.ItemsSource = _libraryView;

        FolderList.ItemsSource = _library.Folders;
        FolderList.SelectedIndex = 0;

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            ToastText.Visibility = Visibility.Collapsed;
            HintText.Visibility = Visibility.Visible;
        };

        // Клик мимо окна прячет его — если окно не закреплено и не открыт диалог.
        Deactivated += (_, _) =>
        {
            if (!_modalOpen && PinButton.IsChecked != true) HideWindow();
        };
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private bool LibraryActive => LibraryTab.IsChecked == true;

    private ListBox ActiveList => LibraryActive ? LibraryList : List;

    private ICollectionView ActiveView => LibraryActive ? _libraryView : _view;

    private string CurrentFolderId =>
        FolderList.SelectedItem is LibraryFolder folder ? folder.Id : string.Empty;

    private bool InCurrentFolder(LibraryItem item) =>
        CurrentFolderId.Length == 0 || item.FolderId == CurrentFolderId;

    // ------------------------------------------------------------ показ ---

    /// <summary>Показать окно у курсора; target — окно, куда потом вставлять.</summary>
    public void ShowFor(IntPtr target)
    {
        _target = target;

        SearchBox.Text = string.Empty;
        // Псевдопапка «Все» переводится на лету: сам список папок не пересоздаётся.
        if (_library.Folders.Count > 0) _library.Folders[0].Name = Strings.FolderAll;
        foreach (var item in _store.Items) item.RefreshMeta();
        ActiveView.Refresh();

        Opacity = 0;
        Show();
        PositionNearCursor();
        Opacity = 1;

        Activate();
        SearchBox.Focus();

        if (ActiveList.Items.Count > 0) ActiveList.SelectedIndex = 0;
        UpdateEmptyState();

        Log.Info($"History window opened: {_store.Items.Count} entries in history, " +
                 $"{_library.Items.Count} in the library.");
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
            Log.Warn($"Could not position the window: {ex.Message}");
        }
    }

    private void HideWindow()
    {
        Hide();
        SearchBox.Text = string.Empty;
    }

    // ------------------------------------------------------------ вкладки ---

    private void Tab_Changed(object sender, RoutedEventArgs e)
    {
        // Событие приходит и при построении окна, когда панелей ещё нет.
        if (HistoryPanel is null || LibraryPanel is null) return;

        HistoryPanel.Visibility = LibraryActive ? Visibility.Collapsed : Visibility.Visible;
        LibraryPanel.Visibility = LibraryActive ? Visibility.Visible : Visibility.Collapsed;
        HintText.Text = LibraryActive ? Strings.LibraryHints : Strings.Hints;

        ActiveView.Refresh();
        if (ActiveList.Items.Count > 0) ActiveList.SelectedIndex = 0;
        UpdateEmptyState();
        SearchBox.Focus();
    }

    private void UpdateEmptyState()
    {
        if (LibraryActive)
        {
            LibraryEmptyHint.Visibility = LibraryList.Items.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            LibraryEmptyHint.Text = _library.Items.Count == 0
                ? Strings.LibraryEmpty
                : Strings.EmptySearch;
        }
        else
        {
            EmptyHint.Visibility = List.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyHint.Text = _store.Items.Count == 0 ? Strings.EmptyHistory : Strings.EmptySearch;
        }

        SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;

        var folder = FolderList.SelectedItem as LibraryFolder;
        RenameFolderButton.IsEnabled = folder is { IsAll: false };
        DeleteFolderButton.IsEnabled = folder is { IsAll: false };
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ActiveView.Refresh();
        if (ActiveList.Items.Count > 0) ActiveList.SelectedIndex = 0;
        UpdateEmptyState();
    }

    private void FolderList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_libraryView is null) return;

        _libraryView.Refresh();
        if (LibraryList.Items.Count > 0) LibraryList.SelectedIndex = 0;
        UpdateEmptyState();
    }

    // --------------------------------------------------------- клавиатура ---

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

            case Key.Tab:
                if (LibraryActive) HistoryTab.IsChecked = true;
                else LibraryTab.IsChecked = true;
                e.Handled = true;
                return;

            case Key.Enter:
                UseSelected(paste: !ctrl);
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
                DeleteSelected();
                e.Handled = true;
                return;

            case Key.P when ctrl && !LibraryActive:
                if (List.SelectedItem is ClipItem toPin)
                {
                    _store.TogglePin(toPin);
                    _view.Refresh();
                    List.SelectedItem = toPin;
                    List.ScrollIntoView(toPin);
                }
                e.Handled = true;
                return;

            case Key.S when ctrl && !LibraryActive:
                SaveSelectedToLibrary();
                e.Handled = true;
                return;

            case Key.F2 when LibraryActive:
                EditSelectedLibraryItem();
                e.Handled = true;
                return;
        }

        // Alt+1…9 — быстрый выбор
        if (alt && e.SystemKey >= Key.D1 && e.SystemKey <= Key.D9)
        {
            var index = e.SystemKey - Key.D1;
            if (index < ActiveList.Items.Count)
            {
                ActiveList.SelectedIndex = index;
                UseSelected(paste: true);
            }
            e.Handled = true;
        }
    }

    private void Move(int delta)
    {
        var list = ActiveList;
        if (list.Items.Count == 0) return;

        var index = Math.Clamp(list.SelectedIndex + delta, 0, list.Items.Count - 1);
        list.SelectedIndex = index;
        list.ScrollIntoView(list.Items[index]);
    }

    // ----------------------------------------------------------- действия ---

    private void UseSelected(bool paste)
    {
        if (LibraryActive)
        {
            if (LibraryList.SelectedItem is LibraryItem item) Use(item.ToPayload(), item.FilesPresent, paste);
        }
        else
        {
            if (List.SelectedItem is ClipItem item) Use(item.ToPayload(), true, paste);
        }
    }

    /// <summary>Скопировать в буфер и, если нужно, вставить в целевое окно.</summary>
    private void Use(ClipboardPayload payload, bool available, bool paste)
    {
        if (!available)
        {
            ShowToast(Strings.ErrFileMissing);
            return;
        }

        HideWindow();

        // Собственную запись в буфер в историю не пишем.
        _monitor.SuppressNext();

        if (!Paster.CopyToClipboard(payload)) return;
        if (paste && _settings.PasteOnSelect) Paster.PasteInto(_target);
    }

    /// <summary>Одиночный клик — просто скопировать запись, окно остаётся открытым.</summary>
    private void CopyOnly(ClipboardPayload payload, bool available)
    {
        if (!available)
        {
            ShowToast(Strings.ErrFileMissing);
            return;
        }

        _monitor.SuppressNext();
        ShowToast(Paster.CopyToClipboard(payload) ? Strings.ToastCopied : Strings.ToastCopyFailed);
    }

    private void DeleteSelected()
    {
        if (LibraryActive)
        {
            if (LibraryList.SelectedItem is not LibraryItem item) return;

            if (!Confirm(Strings.Format("ConfirmDeleteItem", item.Display))) return;

            var index = LibraryList.SelectedIndex;
            _library.Remove(item);
            _libraryView.Refresh();
            if (LibraryList.Items.Count > 0)
                LibraryList.SelectedIndex = Math.Min(index, LibraryList.Items.Count - 1);
        }
        else
        {
            if (List.SelectedItem is not ClipItem item) return;

            var index = List.SelectedIndex;
            _store.Remove(item);
            _view.Refresh();
            if (List.Items.Count > 0)
                List.SelectedIndex = Math.Min(index, List.Items.Count - 1);
        }

        UpdateEmptyState();
    }

    private void SaveSelectedToLibrary()
    {
        if (List.SelectedItem is not ClipItem item) return;

        var errors = new List<string>();
        var saved = _library.AddFromHistory(item, CurrentFolderId, errors);

        ShowToast(saved is not null
            ? Strings.ToastSavedToLibrary
            : errors.FirstOrDefault() ?? Strings.ErrEmptyEntry);
    }

    private void EditSelectedLibraryItem()
    {
        if (LibraryList.SelectedItem is not LibraryItem item) return;

        var dialog = new ItemEditorWindow(_library.Folders, item, CurrentFolderId) { Owner = this };
        if (ShowModal(dialog) != true) return;

        item.Title = dialog.EntryTitle;
        if (item.Kind == ClipKind.Text) item.Text = dialog.EntryText;
        item.FolderId = dialog.FolderId;
        item.Refresh();

        _library.Save();
        _libraryView.Refresh();
        UpdateEmptyState();
    }

    // ------------------------------------------------------------ мышь ---

    private void List_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ItemUnder(List, e) is not ClipItem item) return;

        List.SelectedItem = item;
        CopyOnly(item.ToPayload(), available: true);
    }

    private void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (List.SelectedItem is ClipItem item) Use(item.ToPayload(), true, paste: true);
    }

    private void LibraryList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ItemUnder(LibraryList, e) is not LibraryItem item) return;

        LibraryList.SelectedItem = item;
        CopyOnly(item.ToPayload(), item.FilesPresent);
    }

    private void LibraryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LibraryList.SelectedItem is LibraryItem item)
            Use(item.ToPayload(), item.FilesPresent, paste: true);
    }

    private static object? ItemUnder(ListBox list, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return null;
        return ItemsControl.ContainerFromElement(list, source) is ListBoxItem container
            ? container.DataContext
            : null;
    }

    // ------------------------------------------------------- библиотека ---

    private void AddText_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ItemEditorWindow(_library.Folders, null, CurrentFolderId) { Owner = this };
        if (ShowModal(dialog) != true) return;

        var added = _library.AddText(dialog.EntryText, dialog.EntryTitle, dialog.FolderId);
        SelectInLibrary(added);
    }

    private void AddFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Strings.PickFilesTitle,
            Multiselect = true,
            CheckFileExists = true
        };

        _modalOpen = true;
        var picked = dialog.ShowDialog(this) == true;
        _modalOpen = false;

        if (picked) AddFilesToLibrary(dialog.FileNames);
    }

    private void AddFilesToLibrary(IEnumerable<string> paths)
    {
        var errors = new List<string>();
        var added = _library.AddFiles(paths, CurrentFolderId, errors);

        if (added is not null)
        {
            LibraryTab.IsChecked = true;
            SelectInLibrary(added);
            ShowToast(errors.Count == 0 ? Strings.ToastSavedToLibrary : errors[0]);
        }
        else
        {
            ShowToast(errors.FirstOrDefault() ?? Strings.ErrEmptyEntry);
        }
    }

    private void SelectInLibrary(LibraryItem item)
    {
        _libraryView.Refresh();
        LibraryList.SelectedItem = item;
        LibraryList.ScrollIntoView(item);
        UpdateEmptyState();
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PromptWindow(Strings.ButtonAddFolder, Strings.LabelFolder, Strings.FolderNew)
        {
            Owner = this
        };
        if (ShowModal(dialog) != true) return;

        var folder = _library.AddFolder(dialog.Value);
        FolderList.SelectedItem = folder;
    }

    private void RenameFolder_Click(object sender, RoutedEventArgs e)
    {
        if (FolderList.SelectedItem is not LibraryFolder folder || folder.IsAll) return;

        var dialog = new PromptWindow(Strings.TooltipRenameFolder, Strings.LabelFolder, folder.Name)
        {
            Owner = this
        };
        if (ShowModal(dialog) != true) return;

        _library.RenameFolder(folder, dialog.Value);
    }

    private void DeleteFolder_Click(object sender, RoutedEventArgs e)
    {
        if (FolderList.SelectedItem is not LibraryFolder folder || folder.IsAll) return;
        if (!Confirm(Strings.Format("ConfirmDeleteFolder", folder.Name))) return;

        _library.RemoveFolder(folder);
        FolderList.SelectedIndex = 0;
    }

    // ----------------------------------------------- перетаскивание файлов ---

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        var files = e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = files ? DragDropEffects.Copy : DragDropEffects.None;
        DropOverlay.Visibility = files ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    private void Window_DragLeave(object sender, DragEventArgs e) =>
        DropOverlay.Visibility = Visibility.Collapsed;

    private void Window_Drop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;

        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            AddFilesToLibrary(files.Where(f => File.Exists(f)));

        e.Handled = true;
    }

    // ------------------------------------------------------------ прочее ---

    private bool Confirm(string question)
    {
        _modalOpen = true;
        var result = MessageBox.Show(this, question, "WiClip",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        _modalOpen = false;
        return result == MessageBoxResult.Yes;
    }

    private bool? ShowModal(Window dialog)
    {
        _modalOpen = true;
        var result = dialog.ShowDialog();
        _modalOpen = false;
        Activate();
        return result;
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
        HideWindow();

        // Диалог показывает App: он же применяет язык, тему и пересобирает меню трея.
        // BeginInvoke — чтобы обработчик клика успел завершиться до закрытия окна.
        Dispatcher.BeginInvoke(new Action(() => SettingsRequested?.Invoke()));
    }

    /// <summary>Пользователь нажал шестерёнку — настройки открывает App.</summary>
    public event Action? SettingsRequested;

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
