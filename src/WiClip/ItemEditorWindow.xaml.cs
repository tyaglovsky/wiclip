using System.Windows;

namespace WiClip;

/// <summary>Создание и правка текстовой записи библиотеки.</summary>
public partial class ItemEditorWindow : Window
{
    /// <summary>Название записи (может быть пустым).</summary>
    public string EntryTitle { get; private set; } = string.Empty;

    public string EntryText { get; private set; } = string.Empty;

    /// <summary>Id выбранной папки; пусто — «Все».</summary>
    public string FolderId { get; private set; } = string.Empty;

    /// <param name="folders">Папки библиотеки, включая псевдопапку «Все».</param>
    /// <param name="item">Запись для правки или null для новой.</param>
    /// <param name="currentFolderId">Папка, выбранная в окне библиотеки.</param>
    public ItemEditorWindow(IEnumerable<LibraryFolder> folders, LibraryItem? item, string currentFolderId)
    {
        InitializeComponent();

        Title = item is null ? Strings.EditorTitleNew : Strings.EditorTitleEdit;

        foreach (var folder in folders) FolderBox.Items.Add(folder);

        var wanted = item?.FolderId ?? currentFolderId;
        foreach (LibraryFolder folder in FolderBox.Items)
        {
            if (folder.Id == wanted) FolderBox.SelectedItem = folder;
        }
        FolderBox.SelectedItem ??= FolderBox.Items[0];

        if (item is not null)
        {
            NameBox.Text = item.Title;
            TextBox_.Text = item.Text;
            // Файловую запись правим только по названию и папке.
            TextBox_.IsEnabled = item.Kind == ClipKind.Text;
        }

        Loaded += (_, _) => NameBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (TextBox_.IsEnabled && string.IsNullOrWhiteSpace(TextBox_.Text))
        {
            ErrorText.Text = Strings.ErrEmptyEntry;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        EntryTitle = NameBox.Text.Trim();
        EntryText = TextBox_.Text;
        FolderId = ((LibraryFolder)FolderBox.SelectedItem).Id;

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
