using System.Windows;

namespace MemoryNotesFloating;

public partial class ProjectDialog : Window
{
    public string ProjectName { get; private set; } = "";

    public ProjectDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => NameBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        ProjectName = name;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
