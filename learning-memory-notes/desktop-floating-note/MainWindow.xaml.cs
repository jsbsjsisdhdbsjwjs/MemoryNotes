using System.Collections.ObjectModel;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Windows;
using System.Windows.Input;

namespace MemoryNotesFloating;

public partial class MainWindow : Window
{
    private readonly string _dataPath;
    private readonly ObservableCollection<ProjectItem> _projects = [];
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private AppState _state = new();

    public MainWindow()
    {
        InitializeComponent();
        Left = SystemParameters.WorkArea.Right - Width - 24;
        Top = SystemParameters.WorkArea.Bottom - Height - 24;
        _dataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MemoryNotes",
            "notes.json");
        LoadState();
        ProjectCombo.ItemsSource = _projects;
        ProjectCombo.SelectedValue = _state.ActiveProjectId;
        RenderRecent();
    }

    private void LoadState()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dataPath)!);
        if (File.Exists(_dataPath))
        {
            var json = File.ReadAllText(_dataPath);
            _state = JsonSerializer.Deserialize<AppState>(json, _jsonOptions) ?? new AppState();
        }

        if (_state.Projects.Count == 0)
        {
            var project = new ProjectItem(Guid.NewGuid().ToString("N"), "默认学习项目", DateTimeOffset.Now);
            _state.Projects.Add(project);
            _state.ActiveProjectId = project.Id;
            SaveState();
        }

        _projects.Clear();
        foreach (var project in _state.Projects)
        {
            _projects.Add(project);
        }
    }

    private void SaveState()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dataPath)!);
        File.WriteAllText(_dataPath, JsonSerializer.Serialize(_state, _jsonOptions));
    }

    private void RenderRecent()
    {
        var activeId = _state.ActiveProjectId;
        var items = _state.Notes
            .Where(note => note.ProjectId == activeId)
            .OrderByDescending(note => note.CreatedAt)
            .Take(8)
            .Select(note => $"{note.CreatedAt:MM-dd HH:mm}  {Trim(note.Content)}")
            .ToList();
        RecentList.ItemsSource = items;
    }

    private static string Trim(string value)
    {
        var normalized = value.ReplaceLineEndings(" ");
        return normalized.Length > 42 ? normalized[..42] + "..." : normalized;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var content = NoteText.Text.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            StatusText.Text = "请输入笔记内容";
            return;
        }

        var tags = ExtractTags(content, TagsText.Text);
        var note = new NoteItem(
            Guid.NewGuid().ToString("N"),
            _state.ActiveProjectId,
            content,
            tags,
            BuildLinks(content, tags),
            DateTimeOffset.Now);

        _state.Notes.Add(note);
        SaveState();
        NoteText.Clear();
        TagsText.Clear();
        RenderRecent();
        StatusText.Text = $"已保存：{DateTime.Now:HH:mm:ss}";
    }

    private static List<string> ExtractTags(string content, string typedTags)
    {
        var tags = typedTags
            .Split([',', '，'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => tag.Length > 0)
            .ToList();

        foreach (var word in content.Split(' ', '\r', '\n', '\t'))
        {
            if (word.StartsWith('#') && word.Length > 1)
            {
                tags.Add(word.TrimStart('#').Trim());
            }
        }

        return tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> BuildLinks(string content, List<string> tags)
    {
        var links = new List<string>(tags);
        var terms = content
            .Split([' ', '\r', '\n', '\t', ',', '，', '.', '。', ';', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length is >= 2 and <= 12)
            .Take(12);
        links.AddRange(terms);
        return links.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void AddProjectButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ProjectDialog { Owner = this };
        if (dialog.ShowDialog() != true) return;

        var project = new ProjectItem(Guid.NewGuid().ToString("N"), dialog.ProjectName, DateTimeOffset.Now);
        _state.Projects.Add(project);
        _projects.Add(project);
        _state.ActiveProjectId = project.Id;
        ProjectCombo.SelectedValue = project.Id;
        SaveState();
        RenderRecent();
    }

    private void ProjectCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ProjectCombo.SelectedValue is not string id) return;
        _state.ActiveProjectId = id;
        SaveState();
        RenderRecent();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        PinButton.Content = Topmost ? "置顶" : "普通";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

public record ProjectItem(string Id, string Name, DateTimeOffset CreatedAt);

public record NoteItem(
    string Id,
    string ProjectId,
    string Content,
    List<string> Tags,
    List<string> Links,
    DateTimeOffset CreatedAt);

public sealed class AppState
{
    public string ActiveProjectId { get; set; } = "";
    public List<ProjectItem> Projects { get; set; } = [];
    public List<NoteItem> Notes { get; set; } = [];
}
