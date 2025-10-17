using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using PaperFinch.Controls;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.IO;

namespace PaperFinch.Views;

public partial class MainWindow : Window
{
    private PdfPreviewControl _pdfPreview;
    private TextBox _titleInput;
    private TextBox _contentInput;
    private Button _generateButton;
    private TextBlock _pageInfo;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Width = 900;
        Height = 700;
        Title = "PDF Generator with Live Preview";

        QuestPDF.Settings.License = LicenseType.Community;

        // Create layout
        var mainGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("300,*"),
            RowDefinitions = new RowDefinitions("*")
        };

        // Left panel - inputs
        var leftPanel = new StackPanel
        {
            Margin = new Avalonia.Thickness(10),
            Spacing = 10
        };

        leftPanel.Children.Add(new TextBlock
        {
            Text = "Document Title:",
            FontWeight = Avalonia.Media.FontWeight.Bold
        });

        _titleInput = new TextBox
        {
            Watermark = "Enter title...",
            Text = "Sample Document"
        };
        leftPanel.Children.Add(_titleInput);

        leftPanel.Children.Add(new TextBlock
        {
            Text = "Content:",
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Margin = new Avalonia.Thickness(0, 10, 0, 0)
        });

        _contentInput = new TextBox
        {
            Watermark = "Enter content...",
            Text = "This is a sample PDF document generated with QuestPDF.",
            AcceptsReturn = true,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Height = 200
        };
        leftPanel.Children.Add(_contentInput);

        _generateButton = new Button
        {
            Content = "Generate PDF Preview",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            Margin = new Avalonia.Thickness(0, 10, 0, 0)
        };
        _generateButton.Click += GenerateButton_Click;
        leftPanel.Children.Add(_generateButton);

        _pageInfo = new TextBlock
        {
            Margin = new Avalonia.Thickness(0, 10, 0, 0),
            Text = "No PDF loaded"
        };
        leftPanel.Children.Add(_pageInfo);

        Grid.SetColumn(leftPanel, 0);
        mainGrid.Children.Add(leftPanel);

        // Right panel - PDF preview
        var rightPanel = new Border
        {
            Background = Avalonia.Media.Brushes.LightGray,
            Margin = new Avalonia.Thickness(10),
            Padding = new Avalonia.Thickness(10)
        };

        var scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        _pdfPreview = new PdfPreviewControl
        {
            MaxWidth = 600,
            MaxHeight = 800
        };
        scrollViewer.Content = _pdfPreview;
        rightPanel.Child = scrollViewer;

        Grid.SetColumn(rightPanel, 1);
        mainGrid.Children.Add(rightPanel);

        Content = mainGrid;

        // Generate initial preview
        GeneratePdfPreview();
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        GeneratePdfPreview();
    }

    private void GeneratePdfPreview()
    {
        try
        {
            // Generate PDF using QuestPDF
            byte[] pdfBytes = GeneratePdfDocument(
                _titleInput.Text ?? "Untitled",
                _contentInput.Text ?? ""
            );

            // Load into preview control
            _pdfPreview.LoadPdf(pdfBytes, pageIndex: 0, dpi: 150);

            int pageCount = _pdfPreview.GetPageCount();
            _pageInfo.Text = $"PDF generated: {pageCount} page(s)";
        }
        catch (Exception ex)
        {
            _pageInfo.Text = $"Error: {ex.Message}";
        }
    }

    private byte[] GeneratePdfDocument(string title, string content)
    {
        using var stream = new MemoryStream();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(6, 9, Unit.Inch);
                page.Margin(1, Unit.Inch);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Header()
                    .Text(title)
                    .SemiBold()
                    .FontSize(20)
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
