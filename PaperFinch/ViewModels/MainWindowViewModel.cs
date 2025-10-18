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
using PaperFinch.Components;

namespace PaperFinch.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ThemeService _themeService;
    private readonly FontService _fontService;

    private string _title = "Chapter 1";
    private string _subtitle = "The Beginning";
    private string _content = "This is a sample paragraph with proper formatting. It demonstrates justified text with appropriate line spacing and paragraph indentation.\n\nThis is a second paragraph to show how indentation works across multiple paragraphs in a properly formatted novel.\n\nThis is a third paragraph to further demonstrate the formatting.";
    private string _pageInfo = "No PDF loaded";
    private int _currentPage = 0;
    private int _totalPages = 0;
    private byte[]? _currentPdfBytes;
    private PdfTheme? _selectedTheme;
    private List<PdfTheme> _themes = new();

    // Theme property holders for UI binding
    private double _insideMargin = 0.875;
    private double _outsideMargin = 0.5;
    private double _topMargin = 0.75;
    private double _bottomMargin = 0.75;
    private TrimSizeItem _selectedTrimSize;
    private string _bodyFont = "Times New Roman";
    private int _bodyFontSize = 12;
    private double _lineSpacing = 1.2;
    private double _paragraphIndent = 0.3;
    private string _chapterTitleFont = "Times New Roman";
    private int _chapterTitleFontSize = 24;
    private bool _chapterTitleBold = true;
    private bool _chapterTitleItalic = false;
    private TextAlignment _chapterTitleAlignment = TextAlignment.Center;
    private string _chapterSubtitleFont = "Times New Roman";
    private int _chapterSubtitleFontSize = 18;
    private bool _chapterSubtitleBold = false;
    private bool _chapterSubtitleItalic = true;
    private TextAlignment _chapterSubtitleAlignment = TextAlignment.Center;

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

    // Margin Properties
    public double InsideMargin
    {
        get => _insideMargin;
        set => SetProperty(ref _insideMargin, value);
    }

    public double OutsideMargin
    {
        get => _outsideMargin;
        set => SetProperty(ref _outsideMargin, value);
    }

    public double TopMargin
    {
        get => _topMargin;
        set => SetProperty(ref _topMargin, value);
    }

    public double BottomMargin
    {
        get => _bottomMargin;
        set => SetProperty(ref _bottomMargin, value);
    }

    // Body Text Properties
    public string BodyFont
    {
        get => _bodyFont;
        set => SetProperty(ref _bodyFont, value);
    }

    public int BodyFontSize
    {
        get => _bodyFontSize;
        set => SetProperty(ref _bodyFontSize, value);
    }

    public double LineSpacing
    {
        get => _lineSpacing;
        set => SetProperty(ref _lineSpacing, value);
    }

    public double ParagraphIndent
    {
        get => _paragraphIndent;
        set => SetProperty(ref _paragraphIndent, value);
    }

    // Chapter Title Properties
    public string ChapterTitleFont
    {
        get => _chapterTitleFont;
        set => SetProperty(ref _chapterTitleFont, value);
    }

    public int ChapterTitleFontSize
    {
        get => _chapterTitleFontSize;
        set => SetProperty(ref _chapterTitleFontSize, value);
    }

    public bool ChapterTitleBold
    {
        get => _chapterTitleBold;
        set => SetProperty(ref _chapterTitleBold, value);
    }

    public bool ChapterTitleItalic
    {
        get => _chapterTitleItalic;
        set => SetProperty(ref _chapterTitleItalic, value);
    }

    public TextAlignment ChapterTitleAlignment
    {
        get => _chapterTitleAlignment;
        set => SetProperty(ref _chapterTitleAlignment, value);
    }

    // Chapter Subtitle Properties
    public string ChapterSubtitleFont
    {
        get => _chapterSubtitleFont;
        set => SetProperty(ref _chapterSubtitleFont, value);
    }

    public int ChapterSubtitleFontSize
    {
        get => _chapterSubtitleFontSize;
        set => SetProperty(ref _chapterSubtitleFontSize, value);
    }

    public bool ChapterSubtitleBold
    {
        get => _chapterSubtitleBold;
        set => SetProperty(ref _chapterSubtitleBold, value);
    }

    public bool ChapterSubtitleItalic
    {
        get => _chapterSubtitleItalic;
        set => SetProperty(ref _chapterSubtitleItalic, value);
    }

    public TextAlignment ChapterSubtitleAlignment
    {
        get => _chapterSubtitleAlignment;
        set => SetProperty(ref _chapterSubtitleAlignment, value);
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
    }

    private async Task LoadThemesAsync()
    {
        Themes = await _themeService.LoadAllThemesAsync();
        SelectedTheme = Themes.FirstOrDefault();
    }

    private void ApplyTheme(PdfTheme theme)
    {
        InsideMargin = theme.InsideMargin;
        OutsideMargin = theme.OutsideMargin;
        TopMargin = theme.TopMargin;
        BottomMargin = theme.BottomMargin;
        SelectedTrimSize = TrimSizes.First(ts => ts.Size == theme.TrimSize);
        BodyFont = theme.BodyFont;
        BodyFontSize = theme.BodyFontSize;
        LineSpacing = theme.LineSpacing;
        ParagraphIndent = theme.ParagraphIndent;
        ChapterTitleFont = theme.ChapterTitleFont;
        ChapterTitleFontSize = theme.ChapterTitleFontSize;
        ChapterTitleBold = theme.ChapterTitleBold;
        ChapterTitleItalic = theme.ChapterTitleItalic;
        ChapterTitleAlignment = theme.ChapterTitleAlignment;
        ChapterSubtitleFont = theme.ChapterSubtitleFont;
        ChapterSubtitleFontSize = theme.ChapterSubtitleFontSize;
        ChapterSubtitleBold = theme.ChapterSubtitleBold;
        ChapterSubtitleItalic = theme.ChapterSubtitleItalic;
        ChapterSubtitleAlignment = theme.ChapterSubtitleAlignment;
    }

    private PdfTheme CreateThemeFromCurrentSettings(string name)
    {
        return new PdfTheme
        {
            Name = name,
            TrimSize = SelectedTrimSize.Size,
            InsideMargin = InsideMargin,
            OutsideMargin = OutsideMargin,
            TopMargin = TopMargin,
            BottomMargin = BottomMargin,
            BodyFont = BodyFont,
            BodyFontSize = BodyFontSize,
            LineSpacing = LineSpacing,
            ParagraphIndent = ParagraphIndent,
            ChapterTitleFont = ChapterTitleFont,
            ChapterTitleFontSize = ChapterTitleFontSize,
            ChapterTitleBold = ChapterTitleBold,
            ChapterTitleItalic = ChapterTitleItalic,
            ChapterTitleAlignment = ChapterTitleAlignment,
            ChapterSubtitleFont = ChapterSubtitleFont,
            ChapterSubtitleFontSize = ChapterSubtitleFontSize,
            ChapterSubtitleBold = ChapterSubtitleBold,
            ChapterSubtitleItalic = ChapterSubtitleItalic,
            ChapterSubtitleAlignment = ChapterSubtitleAlignment
        };
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

    private void RenderPageContent(ColumnDescriptor col, string chapterTitle, string chapterSubtitle, string content, PdfTheme theme)
    {
        // Chapter Title
        if (!string.IsNullOrWhiteSpace(chapterTitle))
        {
            col.Item().Text(text =>
            {
                var span = text.Span(chapterTitle);
                span.FontFamily(theme.ChapterTitleFont);
                span.FontSize(theme.ChapterTitleFontSize);
                if (theme.ChapterTitleBold) span.SemiBold();
                if (theme.ChapterTitleItalic) span.Italic();

                ApplyAlignment(text, theme.ChapterTitleAlignment);
            });
            col.Item().PaddingBottom(10);
        }

        // Chapter Subtitle
        if (!string.IsNullOrWhiteSpace(chapterSubtitle))
        {
            col.Item().Text(text =>
            {
                var span = text.Span(chapterSubtitle);
                span.FontFamily(theme.ChapterSubtitleFont);
                span.FontSize(theme.ChapterSubtitleFontSize);
                if (theme.ChapterSubtitleBold) span.SemiBold();
                if (theme.ChapterSubtitleItalic) span.Italic();

                ApplyAlignment(text, theme.ChapterSubtitleAlignment);
            });
            col.Item().PaddingBottom(20);
        }

        // Body Content - split into paragraphs
        var paragraphs = content.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < paragraphs.Length; i++)
        {
            var para = paragraphs[i].Trim();
            if (!string.IsNullOrWhiteSpace(para))
            {
                // First paragraph after chapter title doesn't get indented
                // All subsequent paragraphs get a first-line indent
                var shouldIndent = i > 0;

                if (shouldIndent)
                {
                    // Use Row to create first-line indent
                    col.Item().Row(row =>
                    {
                        // Empty space for indent
                        row.ConstantItem((float)theme.ParagraphIndent, Unit.Inch);

                        // Paragraph text
                        row.RelativeItem().Text(text =>
                        {
                            text.Span(para).FontFamily(theme.BodyFont).FontSize(theme.BodyFontSize);
                            text.Justify();
                        });
                    });
                }
                else
                {
                    // No indent for first paragraph
                    col.Item().Text(text =>
                    {
                        text.Span(para).FontFamily(theme.BodyFont).FontSize(theme.BodyFontSize);
                        text.Justify();
                    });
                }

                if (i < paragraphs.Length - 1)
                {
                    col.Item().PaddingBottom(8);
                }
            }
        }

        // Footer with timestamp
        col.Item().PaddingTop(20).Text(text =>
        {
            text.Span("Generated on: ");
            text.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Italic();
            text.AlignCenter();
        });
    }

    private void ApplyAlignment(TextDescriptor text, TextAlignment alignment)
    {
        switch (alignment)
        {
            case TextAlignment.Left:
                text.AlignLeft();
                break;
            case TextAlignment.Center:
                text.AlignCenter();
                break;
            case TextAlignment.Right:
                text.AlignRight();
                break;
            case TextAlignment.Justify:
                text.Justify();
                break;
        }
    }
}

public class TrimSizeItem
{
    public TrimSize Size { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}