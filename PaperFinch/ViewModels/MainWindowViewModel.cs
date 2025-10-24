using Avalonia.Data.Converters;
using Avalonia.Threading;
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PaperFinch.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ThemeService _themeService;
    private readonly FontService _fontService;
    private readonly ProjectService _projectService;

    private string _pageInfo = "Initializing...";
    private int _currentPage = 0;
    private int _totalPages = 0;
    private byte[]? _currentPdfBytes;
    private PdfTheme? _selectedTheme;
    private List<PdfTheme> _themes = new();
    private TrimSizeItem? _selectedTrimSize;
    private double _zoomLevel = 0.7; // Default scale at 70% (which displays as 100%)
    private bool _isGenerating = false;
    private const int BaseDpi = 150; // Fixed DPI for rendering

    private Dictionary<ChapterViewModel, int> _chapterStartPages = new();
    private Dictionary<string, int> _chapterStartPagesByTitle = new(); // Keyed by chapter title for TOC generation

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
                Dispatcher.UIThread.Post(() =>
                {
                    GoToFirstPageCommand.NotifyCanExecuteChanged();
                    GoToPreviousPageCommand.NotifyCanExecuteChanged();
                    GoToNextPageCommand.NotifyCanExecuteChanged();
                    GoToLastPageCommand.NotifyCanExecuteChanged();
                });
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
                Dispatcher.UIThread.Post(() =>
                {
                    GoToFirstPageCommand.NotifyCanExecuteChanged();
                    GoToPreviousPageCommand.NotifyCanExecuteChanged();
                    GoToNextPageCommand.NotifyCanExecuteChanged();
                    GoToLastPageCommand.NotifyCanExecuteChanged();
                });
            }
        }
    }

    public double ZoomLevel
    {
        get => _zoomLevel;
        set
        {
            if (SetProperty(ref _zoomLevel, value))
            {
                // Re-render current page with new zoom level
                if (CurrentPage > 0)
                {
                    RenderPageAction?.Invoke(CurrentPage - 1);
                }
            }
        }
    }

    public bool IsGenerating
    {
        get => _isGenerating;
        set => SetProperty(ref _isGenerating, value);
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
                DeleteThemeCommand.NotifyCanExecuteChanged();
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
    public List<HeaderContentType> HeaderContentTypes { get; }
    public List<PageNumberPositionItem> PageNumberPositions { get; }

    // Delegate for the View to inject the PDF loader
    public Action<byte[]>? LoadPdfAction { get; set; }
    public Action<int>? RenderPageAction { get; set; }
    public Func<string, Task<string?>>? SaveFileDialogAction { get; set; }
    public Func<string, string, Task<string?>>? PromptForTextAction { get; set; }
    public Func<string, string, Task<bool>>? ConfirmAction { get; set; }

    // Callback for when tabs are switched - set by the View
    public Action<int>? OnTabChanged { get; set; }

    private bool _autoRegenerateEnabled = true;

    public MainWindowViewModel()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        _themeService = new ThemeService();
        _fontService = FontService.Instance;
        _projectService = new ProjectService();

        Project = _project;

        // Wire up chapter selection change notification
        Project.OnChapterSelectionChanged = () =>
        {
            RemoveChapterCommand.NotifyCanExecuteChanged();
            MoveChapterUpCommand.NotifyCanExecuteChanged();
            MoveChapterDownCommand.NotifyCanExecuteChanged();

            // Jump to the selected chapter's page if PDF is loaded
            if (_currentPdfBytes != null)
            {
                JumpToSelectedChapter();
            }
        };

        // Subscribe to property changes on all chapters
        Project.Chapters.CollectionChanged += (s, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (ChapterViewModel chapter in e.NewItems)
                {
                    chapter.PropertyChanged += (cs, ce) =>
                    {
                        // Don't regenerate on Content changes - only on Title, Subtitle, or ExcludeFromPageCount
                        if (ce.PropertyName != nameof(ChapterViewModel.Content))
                        {
                            QueueAutoRegenerate();
                        }
                    };
                }
            }
            QueueAutoRegenerate();
        };

        // Subscribe to existing chapter changes
        foreach (var chapter in Project.Chapters)
        {
            chapter.PropertyChanged += (s, e) =>
            {
                // Don't regenerate on Content changes - only on Title, Subtitle, or ExcludeFromPageCount
                if (e.PropertyName != nameof(ChapterViewModel.Content))
                {
                    QueueAutoRegenerate();
                }
            };
        }

        // Subscribe to project metadata changes
        Project.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != nameof(Project.SelectedChapter))
            {
                QueueAutoRegenerate();
            }
        };

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

        // Populate header content types
        HeaderContentTypes = Enum.GetValues<HeaderContentType>().ToList();

        // Populate page number positions
        PageNumberPositions = Enum.GetValues<PageNumberPosition>()
            .Select(p => new PageNumberPositionItem { Position = p, DisplayName = p.GetDescription() })
            .ToList();

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
            // Temporarily disable auto-regenerate to avoid multiple regenerations
            _autoRegenerateEnabled = false;

            // Apply project metadata and chapters
            Project.ApplyFromModel(project);

            // Apply theme
            if (project.Theme != null)
            {
                ApplyTheme(project.Theme);

                // Update the selected theme in the combo box to match the project's theme
                // The project stores the actual theme name, so we can match directly
                var matchingTheme = Themes.FirstOrDefault(t => t.Name == project.Theme.Name);
                if (matchingTheme != null)
                {
                    // Use the backing field to avoid triggering ApplyTheme again
                    _selectedTheme = matchingTheme;
                    OnPropertyChanged(nameof(SelectedTheme));
                }
                else
                {
                    // Theme not found (maybe it was deleted) - fall back to Default
                    _selectedTheme = Themes.FirstOrDefault(t => t.Name == "Default");
                    OnPropertyChanged(nameof(SelectedTheme));
                }
            }

            PageInfo = $"Project loaded: {project.Name}";

            // Re-enable auto-regenerate and trigger a regeneration
            _autoRegenerateEnabled = true;
            _ = GeneratePdf();
        }
        catch (Exception ex)
        {
            _autoRegenerateEnabled = true;
            PageInfo = $"Failed to load project: {ex.Message}";
        }
    }

    public void ApplyTheme(PdfTheme theme)
    {
        Theme.ApplyFrom(theme, TrimSizes, PageNumberPositions);
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
            if (Project.Chapters.Count == 0)
            {
                PageInfo = "No chapters to generate";
                return;
            }

            IsGenerating = true;
            PageInfo = "Generating PDF...";

            var theme = CreateThemeFromCurrentSettings("Current");

            // Check if any chapter is marked as Table of Contents
            var tocChapter = Project.Chapters.FirstOrDefault(ch => ch.IsTableOfContents);

            byte[] pdfBytes;

            if (tocChapter != null)
            {
                // Two-pass generation for TOC
                // Temporarily disable auto-regeneration to prevent infinite loop
                bool wasAutoRegenerateEnabled = _autoRegenerateEnabled;
                _autoRegenerateEnabled = false;

                try
                {
                    // Clear the title-based dictionary before starting
                    _chapterStartPagesByTitle.Clear();

                    // Pass 1: Generate with placeholder TOC to collect page numbers
                    string originalTocContent = tocChapter.Content;
                    tocChapter.Content = "Generating table of contents...";

                    // Generate first pass and collect page numbers
                    await Task.Run(() => GenerateMultiChapterPdfDocument(Project.Chapters, theme));

                    // Generate TOC content based on collected page numbers
                    // IMPORTANT: Do this BEFORE Pass 2, which will clear _chapterStartPages
                    var tocContent = GenerateTableOfContentsContent(Project.Chapters.ToList(), theme, theme.ShowSubtitlesInTOC);

                    // Update the TOC chapter content
                    tocChapter.Content = tocContent;

                    // Pass 2: Regenerate with actual TOC content
                    pdfBytes = await Task.Run(() => GenerateMultiChapterPdfDocument(Project.Chapters, theme));

                    // Note: We keep the generated TOC content in the chapter
                    // The user can see it and it will be regenerated on next PDF generation
                }
                finally
                {
                    // Re-enable auto-regeneration
                    _autoRegenerateEnabled = wasAutoRegenerateEnabled;
                }
            }
            else
            {
                // Single-pass generation (no TOC)
                pdfBytes = await Task.Run(() => GenerateMultiChapterPdfDocument(Project.Chapters, theme));
            }

            if (LoadPdfAction == null)
            {
                PageInfo = "Preview not available";
                IsGenerating = false;
                return;
            }

            // Invoke the view's loader
            LoadPdfAction.Invoke(pdfBytes);
            _currentPdfBytes = pdfBytes;

            // Keep the current page if we're already viewing, otherwise jump to selected chapter or page 1
            if (CurrentPage == 0 && TotalPages > 0)
            {
                // First time loading - jump to selected chapter or page 1
                JumpToSelectedChapter();
                if (CurrentPage == 0)
                {
                    CurrentPage = 1;
                    RenderPageAction?.Invoke(0);
                    UpdatePageInfo();
                }
            }
            else if (CurrentPage > 0 && CurrentPage <= TotalPages)
            {
                // Already viewing - stay on the same page
                RenderPageAction?.Invoke(CurrentPage - 1);
                UpdatePageInfo();
            }
            else if (TotalPages > 0)
            {
                // Current page is out of bounds, go to page 1
                CurrentPage = 1;
                RenderPageAction?.Invoke(0);
                UpdatePageInfo();
            }

            // Update command state on UI thread
            Dispatcher.UIThread.Post(() => ExportPdfCommand.NotifyCanExecuteChanged());
        }
        catch (Exception ex)
        {
            PageInfo = $"Error: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private void AddChapter()
    {
        var newChapterNumber = Project.Chapters.Count + 1;
        var newChapter = new ChapterViewModel
        {
            Title = $"Chapter {newChapterNumber}",
            Subtitle = string.Empty,
            Content = string.Empty
        };

        Project.Chapters.Add(newChapter);
        Project.SelectedChapter = newChapter;

        PageInfo = $"Added Chapter {newChapterNumber}";
    }

    [RelayCommand(CanExecute = nameof(CanRemoveChapter))]
    private async Task RemoveChapter()
    {
        if (Project.SelectedChapter == null || ConfirmAction == null)
            return;

        var chapterTitle = Project.SelectedChapter.Title;
        bool confirmed = await ConfirmAction.Invoke("Delete Chapter", $"Are you sure you want to delete '{chapterTitle}'?");

        if (!confirmed)
            return;

        var index = Project.Chapters.IndexOf(Project.SelectedChapter);
        Project.Chapters.Remove(Project.SelectedChapter);

        // Select the previous chapter, or the first one if we removed the first
        if (Project.Chapters.Count > 0)
        {
            var newIndex = Math.Max(0, index - 1);
            Project.SelectedChapter = Project.Chapters[newIndex];
        }
        else
        {
            Project.SelectedChapter = null;
        }

        PageInfo = $"Chapter removed. {Project.Chapters.Count} chapter(s) remaining";
        RemoveChapterCommand.NotifyCanExecuteChanged();
        MoveChapterUpCommand.NotifyCanExecuteChanged();
        MoveChapterDownCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemoveChapter() => Project.SelectedChapter != null && Project.Chapters.Count > 1;

    [RelayCommand(CanExecute = nameof(CanMoveChapterUp))]
    private void MoveChapterUp()
    {
        if (Project.SelectedChapter == null)
            return;

        var chapter = Project.SelectedChapter;
        var index = Project.Chapters.IndexOf(chapter);
        if (index > 0)
        {
            Project.Chapters.Move(index, index - 1);
            Project.SelectedChapter = chapter; // Re-select to maintain selection
            PageInfo = "Chapter moved up";
        }
    }

    private bool CanMoveChapterUp()
    {
        if (Project.SelectedChapter == null)
            return false;
        return Project.Chapters.IndexOf(Project.SelectedChapter) > 0;
    }

    [RelayCommand(CanExecute = nameof(CanMoveChapterDown))]
    private void MoveChapterDown()
    {
        if (Project.SelectedChapter == null)
            return;

        var chapter = Project.SelectedChapter;
        var index = Project.Chapters.IndexOf(chapter);
        if (index < Project.Chapters.Count - 1)
        {
            Project.Chapters.Move(index, index + 1);
            Project.SelectedChapter = chapter; // Re-select to maintain selection
            PageInfo = "Chapter moved down";
        }
    }

    private bool CanMoveChapterDown()
    {
        if (Project.SelectedChapter == null)
            return false;
        return Project.Chapters.IndexOf(Project.SelectedChapter) < Project.Chapters.Count - 1;
    }

    [RelayCommand]
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
        if (SelectedTheme == null || SelectedTheme.Name == "Default" || ConfirmAction == null)
            return;

        var themeName = SelectedTheme.Name;
        bool confirmed = await ConfirmAction.Invoke("Delete Theme", $"Are you sure you want to delete the theme '{themeName}'?");

        if (!confirmed)
            return;

        try
        {
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
            // Remember which chapter is currently selected
            var selectedChapterIndex = Project.SelectedChapter != null
                ? Project.Chapters.IndexOf(Project.SelectedChapter)
                : -1;

            // Use the actual selected theme name when saving, so we can restore it on load
            var themeName = SelectedTheme?.Name ?? "Default";
            var theme = CreateThemeFromCurrentSettings(themeName);
            var updatedProject = Project.ToModel(SelectedProject.Name, theme);

            await _projectService.SaveProjectAsync(updatedProject);

            // Update the project in the list
            var index = Projects.FindIndex(p => p.Name == SelectedProject.Name);
            if (index >= 0)
            {
                Projects[index] = updatedProject;

                // Temporarily disable the property change to prevent LoadProjectIntoUI from being called
                var currentProject = _selectedProject;
                _selectedProject = updatedProject;
                OnPropertyChanged(nameof(SelectedProject));

                // Restore the selected chapter
                if (selectedChapterIndex >= 0 && selectedChapterIndex < Project.Chapters.Count)
                {
                    Project.SelectedChapter = Project.Chapters[selectedChapterIndex];
                }
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

            // Use the actual selected theme name when saving, so we can restore it on load
            var themeName = SelectedTheme?.Name ?? "Default";
            var theme = CreateThemeFromCurrentSettings(themeName);
            var newProject = Project.ToModel(projectName, theme);

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
        if (SelectedProject == null || SelectedProject.Name == "Default" || ConfirmAction == null)
            return;

        var projectName = SelectedProject.Name;
        bool confirmed = await ConfirmAction.Invoke("Delete Project", $"Are you sure you want to delete the project '{projectName}'?");

        if (!confirmed)
            return;

        try
        {
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
            string defaultFileName = Project.SelectedChapter != null && !string.IsNullOrWhiteSpace(Project.SelectedChapter.Title)
                ? $"{Project.SelectedChapter.Title}.pdf"
                : "document.pdf";
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
        if (TotalPages > 0)
        {
            var chapterInfo = GetCurrentChapterInfo();
            PageInfo = chapterInfo != null
                ? $"Page {CurrentPage} of {TotalPages} - {chapterInfo}"
                : $"Page {CurrentPage} of {TotalPages}";
        }
        else
        {
            PageInfo = "No PDF loaded";
        }
    }

    private string? GetCurrentChapterInfo()
    {
        foreach (var kvp in _chapterStartPages.OrderByDescending(x => x.Value))
        {
            if (CurrentPage >= kvp.Value)
            {
                return kvp.Key.Title;
            }
        }
        return null;
    }

    private void JumpToSelectedChapter()
    {
        if (Project.SelectedChapter != null && _chapterStartPages.ContainsKey(Project.SelectedChapter))
        {
            CurrentPage = _chapterStartPages[Project.SelectedChapter];
            RenderPageAction?.Invoke(CurrentPage - 1);
            UpdatePageInfo();
        }
    }

    private System.Timers.Timer? _regenerateTimer;

    private void QueueAutoRegenerate()
    {
        if (!_autoRegenerateEnabled)
            return;

        // Debounce rapid changes - wait 500ms after last change before regenerating
        _regenerateTimer?.Stop();
        _regenerateTimer = new System.Timers.Timer(500);
        _regenerateTimer.Elapsed += (s, e) =>
        {
            _regenerateTimer.Stop();
            _ = GeneratePdf();
        };
        _regenerateTimer.AutoReset = false;
        _regenerateTimer.Start();
    }

    public void HandleTabChanged(int tabIndex)
    {
        // Regenerate when switching to the Preview tab (assumed to be index 2)
        if (tabIndex == 2 && _autoRegenerateEnabled)
        {
            _ = GeneratePdf();
        }
    }

    public void UpdatePageCount(int count)
    {
        TotalPages = count;
        UpdatePageInfo();

        // Notify that can-execute status may have changed (on UI thread)
        Dispatcher.UIThread.Post(() =>
        {
            GoToFirstPageCommand.NotifyCanExecuteChanged();
            GoToPreviousPageCommand.NotifyCanExecuteChanged();
            GoToNextPageCommand.NotifyCanExecuteChanged();
            GoToLastPageCommand.NotifyCanExecuteChanged();
        });
    }

    private string GenerateTableOfContentsContent(List<ChapterViewModel> chapters, PdfTheme theme, bool showSubtitles)
    {
        var tocLines = new List<string>();
        int chapterNumber = 0;

        foreach (var chapter in chapters)
        {
            // Skip TOC chapters and excluded chapters
            if (chapter.IsTableOfContents || chapter.ExcludeFromPageCount)
                continue;

            chapterNumber++;

            // Get the page number for this chapter using title as key
            if (_chapterStartPagesByTitle.TryGetValue(chapter.Title, out int pageNum))
            {
                // Use a special separator that we can parse in the renderer
                // Format: "1. Chapter Title|||5" or "1. Chapter Title: Subtitle|||5"
                // We'll use ||| as a separator that won't appear in normal text
                string titleText = $"{chapterNumber}. {chapter.Title}";

                // Add subtitle if enabled and present
                if (showSubtitles && !string.IsNullOrWhiteSpace(chapter.Subtitle))
                {
                    titleText += $": {chapter.Subtitle}"; // Italicize subtitle
                }

                string line = $"{titleText}|||{pageNum}";
                tocLines.Add(line);
            }
        }

        return string.Join("\n\n", tocLines);
    }

    private byte[] GenerateMultiChapterPdfDocument(IEnumerable<ChapterViewModel> chapters, PdfTheme theme)
    {
        using var stream = new MemoryStream();
        var (width, height) = theme.TrimSize.GetDimensions();
        _chapterStartPages.Clear();
        // Don't clear _chapterStartPagesByTitle here - we need it to persist between passes for TOC generation

        var chapterList = chapters.ToList();

        Document.Create(container =>
        {
            // Create a section for numbered pages (all included chapters)
            container.Page(page =>
            {
                page.Size(width, height, Unit.Inch);
                page.PageColor(Colors.White);
                page.MarginTop((float)theme.TopMargin, Unit.Inch);
                page.MarginBottom((float)theme.BottomMargin, Unit.Inch);

                page.DefaultTextStyle(x => x
                    .FontFamily(theme.BodyFont)
                    .FontSize(theme.BodyFontSize)
                    .LineHeight((float)theme.LineSpacing));

                page.Content().Column(col =>
                {
                    bool isFirstChapter = true;

                    // Count how many chapters at the beginning are excluded
                    int excludedChapterCount = 0;
                    foreach (var ch in chapterList)
                    {
                        if (ch.ExcludeFromPageCount)
                            excludedChapterCount++;
                        else
                            break;
                    }

                    foreach (var chapter in chapterList)
                    {
                        // Add page break before each chapter except the first
                        if (!isFirstChapter)
                        {
                            col.Item().PageBreak();
                        }
                        isFirstChapter = false;

                        // Determine if page numbers should be shown for this chapter
                        bool showPageNumbers = !chapter.ExcludeFromPageCount;

                        // Create callback to record the starting page for this chapter
                        var chapterRef = chapter; // Capture for closure
                        Action<int> onFirstPage = (pageNum) =>
                        {
                            _chapterStartPages[chapterRef] = pageNum;
                            _chapterStartPagesByTitle[chapterRef.Title] = pageNum; // Also store by title for TOC
                        };

                        col.Item().Dynamic(new AlternatingMarginContent(
                            chapter.Title,
                            chapter.Subtitle,
                            chapter.Content,
                            theme,
                            -excludedChapterCount, // Pass offset to subtract from page numbers
                            showPageNumbers,
                            Project.BookTitle,
                            Project.BookAuthor,
                            onFirstPage,
                            chapter.IsTableOfContents, // Don't indent paragraphs in TOC
                            chapter.NoIndent, // Don't indent paragraphs if user disabled it
                            chapter.ExcludeFromPageCount // Don't use drop caps in front matter
                        ));
                    }
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

public class PageNumberPositionItem
{
    public PageNumberPosition Position { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

public class ZoomToPercentageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double zoom)
        {
            // Convert internal scale (0.7 = 100%) to display percentage
            // Formula: displayPercentage = (zoom / 0.7) * 100
            double percentage = (zoom / 0.7) * 100.0;
            return $"{percentage:0}%";
        }
        return "100%";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}