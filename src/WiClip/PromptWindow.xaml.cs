using System.Windows;

namespace WiClip;

/// <summary>Однострочный ввод — используется для названий папок.</summary>
public partial class PromptWindow : Window
{
    public string Value { get; private set; } = string.Empty;

    public PromptWindow(string title, string label, string value = "")
    {
        InitializeComponent();

        Title = title;
        LabelText.Text = label;
        ValueBox.Text = value;

        Loaded += (_, _) =>
        {
            ValueBox.Focus();
            ValueBox.SelectAll();
        };
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var text = ValueBox.Text.Trim();
        if (text.Length == 0) return;

        Value = text;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
