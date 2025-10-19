using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaperFinch.Components;
using PaperFinch.Models;
using PaperFinch.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PaperFinch.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ThemeService _themeService;
    private readonly FontService _fontService;
    private readonly ProjectService _projectService;

    private string _title = "Chapter 1";
    private string _subtitle = "The Beginning";
    private string _content = "This is a sample paragraph with proper formatting. It demonstrates justified text with appropriate line spacing and paragraph indentation.\n\nThis is a second paragraph to show how indentation works across multiple paragraphs in a properly formatted novel.\n\nThis is a third paragraph to further demonstrate the formatting.";
    private string _pageInfo = "No PDF loaded";
    private int _currentPage = 0;
    private int _totalPages = 0;
    private byte[]? _currentPdfBytes;
    private PdfTheme? _selectedTheme;
    private List<PdfTheme> _themes = new();
    private TrimSizeItem? _selectedTrimSize;

    private ThemeViewModel _themeVM = new ThemeViewModel();
    public ThemeViewModel Theme
    {
        get => _themeVM;
        set => SetProperty(ref _themeVM, value);
    }

    private ProjectViewModel _project = new ProjectViewModel();
    public ProjectViewModel Project
    {
        get => _project;
        set => SetProperty(ref _project, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Subtitle
    {
        get => _subtitle;
        set => SetProperty(ref _subtitle, value);
    }

    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    public string PageInfo
    {
        get => _pageInfo;
        set => SetProperty(ref _pageInfo, value);
    }

    public int CurrentPage
    {
        get => _currentPage;
        set
        {
            if (SetProperty(ref _currentPage, value))
            {
                GoToFirstPageCommand.NotifyCanExecuteChanged();
                GoToPreviousPageCommand.NotifyCanExecuteChanged();
                GoToNextPageCommand.NotifyCanExecuteChanged();
                GoToLastPageCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public int TotalPages
    {
        get => _totalPages;
        set
        {
            if (SetProperty(ref _totalPages, value))
            {
                GoToFirstPageCommand.NotifyCanExecuteChanged();
                GoToPreviousPageCommand.NotifyCanExecuteChanged();
                GoToNextPageCommand.NotifyCanExecuteChanged();
                GoToLastPageCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public TrimSizeItem SelectedTrimSize
    {
        get => _selectedTrimSize;
        set => SetProperty(ref _selectedTrimSize, value);
    }

    public PdfTheme? SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value) && value != null)
            {
                ApplyTheme(value);
                SaveThemeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public List<PdfTheme> Themes
    {
        get => _themes;
        set => SetProperty(ref _themes, value);
    }

    // UI-facing project list (ComboBox ItemsSource)
    private List<Models.Project> _projects = new();
    public List<Models.Project> Projects
    {
        get => _projects;
        set => SetProperty(ref _projects, value);
    }

    // Currently selected project
    private Models.Project? _selectedProject;
    public Models.Project? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (SetProperty(ref _selectedProject, value) && value != null)
            {
                LoadProjectIntoUI(value);
                SaveProjectCommand.NotifyCanExecuteChanged();
                DeleteProjectCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public List<TrimSizeItem> TrimSizes { get; }
    public List<string> AvailableFonts { get; }
    public List<TextAlignment> TextAlignments { get; }

    // Delegate for the View to inject the PDF loader
    public Action<byte[]>? LoadPdfAction { get; set; }
    public Action<int>? RenderPageAction { get; set; }
    public Func<string, Task<string?>>? SaveFileDialogAction { get; set; }
    public Func<string, string, Task<string?>>? PromptForTextAction { get; set; }

    public MainWindowViewModel()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        _themeService = new ThemeService();
        _fontService = FontService.Instance;
        _projectService = new ProjectService();

        Project = _project;

        // Populate trim sizes from enum
        TrimSizes = Enum.GetValues<TrimSize>()
            .Select(ts => new TrimSizeItem { Size = ts, DisplayName = ts.GetDescription() })
            .ToList();

        // Set default selection
        _selectedTrimSize = TrimSizes.First(ts => ts.Size == TrimSize.Standard_6x9);

        // Populate available fonts
        AvailableFonts = _fontService.GetAvailableFonts();

        // Populate text alignments
        TextAlignments = Enum.GetValues<TextAlignment>().ToList();

        // Load themes asynchronously
        _ = LoadThemesAsync();

        // Load projects asynchronously
        _ = LoadProjectsListAsync();
    }

    private async Task LoadThemesAsync()
    {
        Themes = await _themeService.LoadAllThemesAsync();
        SelectedTheme = Themes.FirstOrDefault();
    }

    // Load projects list on startup
    private async Task LoadProjectsListAsync()
    {
        Projects = await _projectService.LoadAllProjectsAsync();
        SelectedProject = Projects.FirstOrDefault();
    }

    // Apply a loaded project to the UI
    private void LoadProjectIntoUI(Models.Project project)
    {
        try
        {
            // Apply project metadata
            Project.ApplyFromModel(project);

            // Apply theme
            if (project.Theme != null)
                ApplyTheme(project.Theme);

            // Apply chapter content
            Title = project.ChapterTitle ?? Title;
            Subtitle = project.ChapterSubtitle ?? Subtitle;
            Content = project.Content ?? Content;

            PageInfo = $"Project loaded: {project.Name}";
        }
        catch (Exception ex)
        {
            PageInfo = $"Failed to load project: {ex.Message}";
        }
    }

    public void ApplyTheme(PdfTheme theme)
    {
        Theme.ApplyFrom(theme, TrimSizes);
    }

    private PdfTheme CreateThemeFromCurrentSettings(string name)
    {
        return Theme.ToPdfTheme(name);
    }

    [RelayCommand]
    private async Task GeneratePdf()
    {
        try
        {
            PageInfo = "Generating PDF...";

            var theme = CreateThemeFromCurrentSettings("Current");

            // Generate the PDF on a thread-pool thread so UI stays responsive
            byte[] pdfBytes = await Task.Run(() => GeneratePdfDocument(Title, Subtitle, Content, theme));

            if (LoadPdfAction == null)
            {
                PageInfo = "Preview not available";
                return;
            }

            // Invoke the view's loader
            LoadPdfAction.Invoke(pdfBytes);
            _currentPdfBytes = pdfBytes;
            CurrentPage = 1;
            ExportPdfCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            PageInfo = $"Error: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveTheme))]
    private async Task SaveTheme()
    {
        if (SelectedTheme == null || SelectedTheme.Name == "Default")
            return;

        try
        {
            var updatedTheme = CreateThemeFromCurrentSettings(SelectedTheme.Name);
            await _themeService.SaveThemeAsync(updatedTheme);

            // Update the theme in the list
            var index = Themes.FindIndex(t => t.Name == SelectedTheme.Name);
            if (index >= 0)
            {
                Themes[index] = updatedTheme;
                SelectedTheme = updatedTheme;
            }

            PageInfo = $"Theme '{SelectedTheme.Name}' saved";
        }
        catch (Exception ex)
        {
            PageInfo = $"Error saving theme: {ex.Message}";
        }
    }

    private bool CanSaveTheme() => SelectedTheme != null && SelectedTheme.Name != "Default";

    [RelayCommand]
    private async Task SaveAsNewTheme()
    {
        if (PromptForTextAction == null)
            return;

        try
        {
            var themeName = await PromptForTextAction.Invoke("Save As New Theme", "Enter theme name:");

            if (string.IsNullOrWhiteSpace(themeName))
                return;

            if (await _themeService.ThemeExistsAsync(themeName))
            {
                PageInfo = $"Theme '{themeName}' already exists";
                return;
            }

            var newTheme = CreateThemeFromCurrentSettings(themeName);
            await _themeService.SaveThemeAsync(newTheme);

            // Reload themes and select the new one
            Themes = await _themeService.LoadAllThemesAsync();
            SelectedTheme = Themes.FirstOrDefault(t => t.Name == themeName);

            PageInfo = $"Theme '{themeName}' created";
        }
        catch (Exception ex)
        {
            PageInfo = $"Error creating theme: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteTheme))]
    private async Task DeleteTheme()
    {
        if (SelectedTheme == null || SelectedTheme.Name == "Default")
            return;

        try
        {
            var themeName = SelectedTheme.Name;
            await _themeService.DeleteThemeAsync(themeName);

            // Reload themes and select default
            Themes = await _themeService.LoadAllThemesAsync();
            SelectedTheme = Themes.FirstOrDefault();

            PageInfo = $"Theme '{themeName}' deleted";
        }
        catch (Exception ex)
        {
            PageInfo = $"Error deleting theme: {ex.Message}";
        }
    }

    private bool CanDeleteTheme() => SelectedTheme != null && SelectedTheme.Name != "Default";

    [RelayCommand(CanExecute = nameof(CanSaveProject))]
    private async Task SaveProjectAsync()
    {
        if (SelectedProject == null || SelectedProject.Name == "Default")
        {
            await SaveAsNewProjectAsync();
            return;
        }

        try
        {
            var theme = CreateThemeFromCurrentSettings("ProjectTheme");
            var updatedProject = Project.ToModel(SelectedProject.Name, theme, Title, Subtitle, Content);

            await _projectService.SaveProjectAsync(updatedProject);

            // Update the project in the list
            var index = Projects.FindIndex(p => p.Name == SelectedProject.Name);
            if (index >= 0)
            {
                Projects[index] = updatedProject;
                SelectedProject = updatedProject;
            }

            PageInfo = $"Project '{SelectedProject.Name}' saved";
        }
        catch (Exception ex)
        {
            PageInfo = $"Save error: {ex.Message}";
        }
    }

    private bool CanSaveProject() => SelectedProject != null && SelectedProject.Name != "Default";

    [RelayCommand]
    private async Task SaveAsNewProjectAsync()
    {
        if (PromptForTextAction == null)
            return;

        try
        {
            var projectName = await PromptForTextAction.Invoke("Save As New Project", "Enter project name:");

            if (string.IsNullOrWhiteSpace(projectName))
                return;

            if (await _projectService.ProjectExistsAsync(projectName))
            {
                PageInfo = $"Project '{projectName}' already exists";
                return;
            }

            var theme = CreateThemeFromCurrentSettings("ProjectTheme");
            var newProject = Project.ToModel(projectName, theme, Title, Subtitle, Content);

            await _projectService.SaveProjectAsync(newProject);

            // Reload projects and select the new one
            Projects = await _projectService.LoadAllProjectsAsync();
            SelectedProject = Projects.FirstOrDefault(p => p.Name == projectName);

            PageInfo = $"Project '{projectName}' created";
        }
        catch (Exception ex)
        {
            PageInfo = $"Save As error: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteProject))]
    private async Task DeleteProjectAsync()
    {
        if (SelectedProject == null || SelectedProject.Name == "Default")
            return;

        try
        {
            var projectName = SelectedProject.Name;
            await _projectService.DeleteProjectAsync(projectName);

            // Reload projects and select default
            Projects = await _projectService.LoadAllProjectsAsync();
            SelectedProject = Projects.FirstOrDefault();

            PageInfo = $"Project '{projectName}' deleted";
        }
        catch (Exception ex)
        {
            PageInfo = $"Delete error: {ex.Message}";
        }
    }

    private bool CanDeleteProject() => SelectedProject != null && SelectedProject.Name != "Default";

    [RelayCommand(CanExecute = nameof(CanExportPdf))]
    private async Task ExportPdf()
    {
        if (_currentPdfBytes == null || SaveFileDialogAction == null)
            return;

        try
        {
            string defaultFileName = string.IsNullOrWhiteSpace(Title) ? "document.pdf" : $"{Title}.pdf";
            string? filePath = await SaveFileDialogAction.Invoke(defaultFileName);

            if (!string.IsNullOrEmpty(filePath))
            {
                await File.WriteAllBytesAsync(filePath, _currentPdfBytes);
                PageInfo = $"PDF exported to: {Path.GetFileName(filePath)}";
            }
        }
        catch (Exception ex)
        {
            PageInfo = $"Export error: {ex.Message}";
        }
    }

    private bool CanExportPdf() => _currentPdfBytes != null && _currentPdfBytes.Length > 0;

    [RelayCommand(CanExecute = nameof(CanGoToFirstPage))]
    private void GoToFirstPage()
    {
        CurrentPage = 1;
        RenderPageAction?.Invoke(CurrentPage - 1);
        UpdatePageInfo();
    }

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    private void GoToPreviousPage()
    {
        CurrentPage--;
        RenderPageAction?.Invoke(CurrentPage - 1);
        UpdatePageInfo();
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    private void GoToNextPage()
    {
        CurrentPage++;
        RenderPageAction?.Invoke(CurrentPage - 1);
        UpdatePageInfo();
    }

    [RelayCommand(CanExecute = nameof(CanGoToLastPage))]
    private void GoToLastPage()
    {
        CurrentPage = TotalPages;
        RenderPageAction?.Invoke(CurrentPage - 1);
        UpdatePageInfo();
    }

    private bool CanGoToFirstPage() => CurrentPage > 1;
    private bool CanGoToPreviousPage() => CurrentPage > 1;
    private bool CanGoToNextPage() => CurrentPage < TotalPages;
    private bool CanGoToLastPage() => CurrentPage < TotalPages;

    private void UpdatePageInfo()
    {
        PageInfo = TotalPages > 0 ? $"Page {CurrentPage} of {TotalPages}" : "No PDF loaded";
    }

    public void UpdatePageCount(int count)
    {
        TotalPages = count;
        UpdatePageInfo();

        // Notify that can-execute status may have changed
        GoToFirstPageCommand.NotifyCanExecuteChanged();
        GoToPreviousPageCommand.NotifyCanExecuteChanged();
        GoToNextPageCommand.NotifyCanExecuteChanged();
        GoToLastPageCommand.NotifyCanExecuteChanged();
    }

    private byte[] GeneratePdfDocument(string chapterTitle, string chapterSubtitle, string content, PdfTheme theme)
    {
        using var stream = new MemoryStream();
        var (width, height) = theme.TrimSize.GetDimensions();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(width, height, Unit.Inch);
                page.PageColor(Colors.White);

                // Set top and bottom margins
                page.MarginTop((float)theme.TopMargin, Unit.Inch);
                page.MarginBottom((float)theme.BottomMargin, Unit.Inch);

                // Default text style
                page.DefaultTextStyle(x => x
                    .FontFamily(theme.BodyFont)
                    .FontSize(theme.BodyFontSize)
                    .LineHeight((float)theme.LineSpacing));

                // Use dynamic component that manages its own pagination
                page.Content().Dynamic(new AlternatingMarginContent(
                    chapterTitle,
                    chapterSubtitle,
                    content,
                    theme
                ));

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });
            });
        }).GeneratePdf(stream);

        return stream.ToArray();
    }
}

public class TrimSizeItem
{
    public TrimSize Size { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}