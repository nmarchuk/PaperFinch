using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PaperFinch.Models;
using PaperFinch.Services;

namespace PaperFinch.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ThemeService _themeService;

    private string _title = "Sample Document";
    private string _content = "This is a sample PDF document generated with QuestPDF.";
    private string _pageInfo = "No PDF loaded";
    private int _currentPage = 0;
    private int _totalPages = 0;
    private int _fontSize = 12;
    private double _marginSize = 1.0;
    private TrimSizeItem _selectedTrimSize;
    private byte[]? _currentPdfBytes;
    private PdfTheme? _selectedTheme;
    private List<PdfTheme> _themes = new();

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
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

    public int FontSize
    {
        get => _fontSize;
        set => SetProperty(ref _fontSize, value);
    }

    public double MarginSize
    {
        get => _marginSize;
        set => SetProperty(ref _marginSize, value);
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

    public List<TrimSizeItem> TrimSizes { get; }

    // Delegate for the View to inject the PDF loader
    public Action<byte[]>? LoadPdfAction { get; set; }
    public Action<int>? RenderPageAction { get; set; }
    public Func<string, Task<string?>>? SaveFileDialogAction { get; set; }
    public Func<string, string, Task<string?>>? PromptForTextAction { get; set; }

    public MainWindowViewModel()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        _themeService = new ThemeService();

        // Populate trim sizes from enum
        TrimSizes = Enum.GetValues<TrimSize>()
            .Select(ts => new TrimSizeItem { Size = ts, DisplayName = ts.GetDescription() })
            .ToList();

        // Set default selection
        _selectedTrimSize = TrimSizes.First(ts => ts.Size == TrimSize.Standard_6x9);

        // Load themes asynchronously
        _ = LoadThemesAsync();
    }

    private async Task LoadThemesAsync()
    {
        Themes = await _themeService.LoadAllThemesAsync();
        SelectedTheme = Themes.FirstOrDefault();
    }

    private void ApplyTheme(PdfTheme theme)
    {
        FontSize = theme.FontSize;
        MarginSize = theme.MarginSize;
        SelectedTrimSize = TrimSizes.First(ts => ts.Size == theme.TrimSize);
    }

    private PdfTheme CreateThemeFromCurrentSettings(string name)
    {
        return new PdfTheme
        {
            Name = name,
            FontSize = FontSize,
            MarginSize = MarginSize,
            TrimSize = SelectedTrimSize.Size
        };
    }

    [RelayCommand]
    private async Task GeneratePdf()
    {
        try
        {
            PageInfo = "Generating PDF...";

            // Generate the PDF on a thread-pool thread so UI stays responsive
            byte[] pdfBytes = await Task.Run(() => GeneratePdfDocument(Title, Content, FontSize, MarginSize, SelectedTrimSize.Size));

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

    private byte[] GeneratePdfDocument(string title, string content, int fontSize, double marginSize, TrimSize trimSize)
    {
        using var stream = new MemoryStream();
        var (width, height) = trimSize.GetDimensions();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(width, height, Unit.Inch);
                page.Margin((float)marginSize, Unit.Inch);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(fontSize));

                page.Header()
                    .Text(title)
                    .SemiBold()
                    .FontSize(fontSize + 8)
                    .FontColor(Colors.Blue.Medium);

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(col =>
                    {
                        col.Item().Text(content);
                        col.Item().PaddingTop(20).Text(text =>
                        {
                            text.Span("Generated on: ");
                            text.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                                .Italic();
                        });
                    });

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