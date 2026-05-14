using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using System.Windows;
using System.Windows.Input;

namespace MemoryNotesFloating;

public partial class MainWindow : Window
{
    private static readonly char[] TagSeparators = [',', ';', ' ', '\r', '\n', '\t'];
    private static readonly char[] TermSeparators = [' ', '\r', '\n', '\t', ',', '.', ';', ':', '/', '\\', '|'];

    private readonly string _dataPath;
    private readonly string _obsidianVaultPath;
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
        _dataPath = ResolveDataPath();
        _obsidianVaultPath = ResolveObsidianVaultPath();
        LoadState();
        ProjectCombo.ItemsSource = _projects;
        ProjectCombo.SelectedValue = _state.ActiveProjectId;
        RenderRecent();
    }

    private static string ResolveDataPath()
    {
        const string preferredRoot = @"E:\";
        if (Directory.Exists(preferredRoot))
        {
            var preferredDirectory = Path.Combine(preferredRoot, "MemoryNotes", "data");
            try
            {
                Directory.CreateDirectory(preferredDirectory);
                return Path.Combine(preferredDirectory, "notes.json");
            }
            catch (UnauthorizedAccessException)
            {
                // Fall back below when the drive exists but the app cannot write there.
            }
            catch (IOException)
            {
                // Fall back below when the drive is temporarily unavailable.
            }
        }

        var fallbackDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MemoryNotes");
        Directory.CreateDirectory(fallbackDirectory);
        return Path.Combine(fallbackDirectory, "notes.json");
    }

    private static string ResolveObsidianVaultPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Obsidian Vault");
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
            var project = new ProjectItem(Guid.NewGuid().ToString("N"), "Default Study Project", DateTimeOffset.Now);
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
        File.WriteAllText(_dataPath, JsonSerializer.Serialize(_state, _jsonOptions), Encoding.UTF8);
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
            StatusText.Text = "Enter note content.";
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

        var project = _state.Projects.FirstOrDefault(item => item.Id == _state.ActiveProjectId);
        SyncNoteToObsidian(project, note);

        NoteText.Clear();
        TagsText.Clear();
        RenderRecent();
        StatusText.Text = $"Saved {DateTime.Now:HH:mm:ss}";
    }

    private static List<string> ExtractTags(string content, string typedTags)
    {
        var tags = typedTags
            .Split(TagSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => tag.Length > 0)
            .ToList();

        foreach (Match match in Regex.Matches(content, @"#([\p{L}\p{N}_-]+)"))
        {
            tags.Add(match.Groups[1].Value.Trim());
        }

        return tags
            .Select(NormalizeTag)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> BuildLinks(string content, List<string> tags)
    {
        var links = new List<string>(tags);
        var terms = content
            .Split(TermSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length is >= 2 and <= 16)
            .Take(16);
        links.AddRange(terms);
        return links.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void SyncNoteToObsidian(ProjectItem? project, NoteItem note)
    {
        try
        {
            if (!Directory.Exists(_obsidianVaultPath))
            {
                return;
            }

            var projectName = string.IsNullOrWhiteSpace(project?.Name) ? "Default Study Project" : project.Name;
            var safeProjectName = SafeFileName(projectName);
            var slug = NormalizeTag(projectName);
            var projectTags = new List<string> { "project", "memorynotes", "auto-generated", slug };
            var opsTags = new List<string> { "ops", "memorynotes", "auto-generated", slug };
            projectTags.AddRange(note.Tags);
            opsTags.AddRange(note.Tags);

            var projectsDirectory = Path.Combine(_obsidianVaultPath, "Projects");
            var opsDirectory = Path.Combine(_obsidianVaultPath, "Ops");
            Directory.CreateDirectory(projectsDirectory);
            Directory.CreateDirectory(opsDirectory);

            var projectPath = Path.Combine(projectsDirectory, $"{safeProjectName}.md");
            var opsPath = Path.Combine(opsDirectory, $"{safeProjectName} Ops.md");

            EnsureProjectNote(projectPath, projectName, projectTags);
            AppendOpsEntry(opsPath, projectName, opsTags, note);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            StatusText.Text = "Saved locally; Obsidian sync failed.";
        }
    }

    private static void EnsureProjectNote(string path, string projectName, List<string> tags)
    {
        if (File.Exists(path))
        {
            MergeFrontmatterTags(path, tags);
            return;
        }

        var content = new StringBuilder();
        WriteFrontmatter(content, tags, "active");
        content.AppendLine($"# {projectName}");
        content.AppendLine();
        content.AppendLine("## Purpose");
        content.AppendLine();
        content.AppendLine("Auto-created from MemoryNotes desktop floating window.");
        content.AppendLine();
        content.AppendLine("## Recent Notes");
        content.AppendLine();
        content.AppendLine($"- See [[Ops/{projectName} Ops]] for captured note operations.");
        File.WriteAllText(path, content.ToString(), Encoding.UTF8);
    }

    private static void AppendOpsEntry(string path, string projectName, List<string> tags, NoteItem note)
    {
        if (!File.Exists(path))
        {
            var header = new StringBuilder();
            WriteFrontmatter(header, tags, null);
            header.AppendLine($"# {projectName} Ops");
            header.AppendLine();
            header.AppendLine("## Captured Notes");
            header.AppendLine();
            File.WriteAllText(path, header.ToString(), Encoding.UTF8);
        }
        else
        {
            MergeFrontmatterTags(path, tags);
        }

        var entry = new StringBuilder();
        entry.AppendLine($"### {note.CreatedAt.LocalDateTime:yyyy-MM-dd HH:mm:ss}");
        entry.AppendLine();
        entry.AppendLine($"Tags: {string.Join(" ", note.Tags.Select(tag => "#" + tag))}");
        entry.AppendLine();
        entry.AppendLine("Content:");
        entry.AppendLine();
        entry.AppendLine(note.Content);
        entry.AppendLine();
        entry.AppendLine($"Links: {string.Join(", ", note.Links.Take(16))}");
        entry.AppendLine();
        File.AppendAllText(path, entry.ToString(), Encoding.UTF8);
    }

    private static void WriteFrontmatter(StringBuilder builder, List<string> tags, string? status)
    {
        builder.AppendLine("---");
        builder.AppendLine("tags:");
        foreach (var tag in tags.Select(NormalizeTag).Where(tag => tag.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"  - {tag}");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            builder.AppendLine($"status: {status}");
        }

        builder.AppendLine($"updated: {DateTime.Now:yyyy-MM-dd}");
        builder.AppendLine("---");
        builder.AppendLine();
    }

    private static void MergeFrontmatterTags(string path, List<string> tags)
    {
        var text = File.ReadAllText(path, Encoding.UTF8);
        if (!text.StartsWith("---", StringComparison.Ordinal))
        {
            var newFile = new StringBuilder();
            WriteFrontmatter(newFile, tags, null);
            newFile.Append(text);
            File.WriteAllText(path, newFile.ToString(), Encoding.UTF8);
            return;
        }

        var end = text.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end < 0)
        {
            return;
        }

        var frontmatter = text[..(end + 4)];
        var body = text[(end + 4)..].TrimStart('\r', '\n');
        var existingTags = Regex.Matches(frontmatter, @"^\s*-\s*([A-Za-z0-9_-]+)\s*$", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .ToList();
        var mergedTags = existingTags
            .Concat(tags.Select(NormalizeTag))
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var statusMatch = Regex.Match(frontmatter, @"^status:\s*(.+)$", RegexOptions.Multiline);
        var updatedFile = new StringBuilder();
        WriteFrontmatter(updatedFile, mergedTags, statusMatch.Success ? statusMatch.Groups[1].Value.Trim() : null);
        updatedFile.Append(body);
        File.WriteAllText(path, updatedFile.ToString(), Encoding.UTF8);
    }

    private static string NormalizeTag(string value)
    {
        var normalized = value.Trim().ToLower(CultureInfo.InvariantCulture);
        normalized = Regex.Replace(normalized, @"[^\p{L}\p{N}_-]+", "-");
        normalized = normalized.Trim('-');
        return normalized.Length > 36 ? normalized[..36] : normalized;
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Default Study Project" : cleaned;
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
        PinButton.Content = Topmost ? "Top" : "Normal";
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
