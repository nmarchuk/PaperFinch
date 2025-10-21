using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using PaperFinch.ViewModels;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TextMateSharp.Grammars;

namespace PaperFinch.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Wait for DataContext to be set, then attach actions
        this.Opened += OnWindowOpened;
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            // Wire up AvaloniaEdit TextEditor to ViewModel
            SetupTextEditor(vm);


            // Attach LoadPdfAction to the actual VM instance used as DataContext
            vm.LoadPdfAction = pdfBytes =>
            {
                // Marshal UI updates to the UI thread
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        PdfPreview.LoadPdf(pdfBytes, 0, 150, vm.ZoomLevel);
                        int pages = PdfPreview.GetPageCount();
                        vm.UpdatePageCount(pages);
                    }
                    catch (Exception ex)
                    {
                        vm.PageInfo = $"Preview error: {ex.Message}";
                    }
                });
            };

            // Attach RenderPageAction for page navigation
            vm.RenderPageAction = pageIndex =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        PdfPreview.RenderPage(pageIndex, 150, vm.ZoomLevel);
                    }
                    catch (Exception ex)
                    {
                        vm.PageInfo = $"Error rendering page: {ex.Message}";
                    }
                });
            };

            // Attach SaveFileDialogAction for exporting PDFs
            vm.SaveFileDialogAction = async (defaultFileName) =>
            {
                var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save PDF File",
                    SuggestedFileName = defaultFileName,
                    DefaultExtension = "pdf",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("PDF Document")
                        {
                            Patterns = new[] { "*.pdf" }
                        }
                    }
                });

                return file?.Path.LocalPath;
            };

            // Attach PromptForTextAction for theme naming
            vm.PromptForTextAction = async (title, message) =>
            {
                var dialog = new TextInputDialog
                {
                    Title = title,
                    Message = message
                };

                return await dialog.ShowDialog<string?>(this);
            };

            // Attach ConfirmAction for delete confirmations
            vm.ConfirmAction = async (title, message) =>
            {
                var dialog = new ConfirmationDialog
                {
                    Title = title,
                    Message = message
                };

                var result = await dialog.ShowDialog<bool?>(this);
                return result ?? false;
            };

            // Optional: Auto-generate on startup
            vm.GeneratePdfCommand.Execute(null);
        }
    }

    private void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && sender is TabControl tabControl)
        {
            vm.HandleTabChanged(tabControl.SelectedIndex);
        }
    }

    private void SetupTextEditor(MainWindowViewModel vm)
    {
        // Initialize the TextEditor with a Document - CRITICAL for AvaloniaEdit to work
        if (ContentEditor.Document == null)
        {
            ContentEditor.Document = new TextDocument();
        }

        // Setup TextMate for Markdown syntax highlighting
        var registryOptions = new RegistryOptions(ThemeName.Light);
        var textMateInstallation = ContentEditor.InstallTextMate(registryOptions);
        textMateInstallation.SetGrammar(registryOptions.GetScopeByLanguageId(registryOptions.GetLanguageByExtension(".md").Id));

        // Add custom colorizer for formatted text (shows in different colors)
        ContentEditor.TextArea.TextView.LineTransformers.Add(new MarkdownFormattingColorizer());

        // Sync editor content when chapter selection changes
        // Save the existing callback from ViewModel
        var existingCallback = vm.Project.OnChapterSelectionChanged;

        vm.Project.OnChapterSelectionChanged = () =>
        {
            // Marshal everything to UI thread to avoid threading issues
            Dispatcher.UIThread.Post(() =>
            {
                // Call the existing callback first (handles button states and page navigation)
                existingCallback?.Invoke();

                // Then update the text editor content
                if (vm.Project.SelectedChapter != null && ContentEditor.Document != null)
                {
                    ContentEditor.Document.Text = vm.Project.SelectedChapter.Content ?? string.Empty;
                }
                else if (ContentEditor.Document != null)
                {
                    ContentEditor.Document.Text = string.Empty;
                }
            });
        };

        // Sync ViewModel when editor text changes
        if (ContentEditor.Document != null)
        {
            ContentEditor.Document.TextChanged += (s, e) =>
            {
                if (vm.Project.SelectedChapter != null)
                {
                    vm.Project.SelectedChapter.Content = ContentEditor.Document.Text;
                }
            };
        }

        // Load initial content
        if (vm.Project.SelectedChapter != null && ContentEditor.Document != null)
        {
            ContentEditor.Document.Text = vm.Project.SelectedChapter.Content ?? string.Empty;
        }
    }

    // Custom colorizer to highlight Markdown formatted text
    public class MarkdownFormattingColorizer : DocumentColorizingTransformer
    {
        protected override void ColorizeLine(DocumentLine line)
        {
            if (CurrentContext.Document == null) return;

            var lineText = CurrentContext.Document.GetText(line);

            // Color bold text: **text** or __text__
            ColorizePattern(line, lineText, @"\*\*(.+?)\*\*", Colors.DarkBlue);
            ColorizePattern(line, lineText, @"__(.+?)__", Colors.DarkBlue);

            // Color italic text: *text* or _text_
            ColorizePattern(line, lineText, @"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", Colors.DarkGreen);
            ColorizePattern(line, lineText, @"(?<!_)_(?!_)(.+?)(?<!_)_(?!_)", Colors.DarkGreen);

            // Color underline text: ++text++
            ColorizePattern(line, lineText, @"\+\+(.+?)\+\+", Colors.DarkOrange);
        }

        private void ColorizePattern(DocumentLine line, string lineText, string pattern, Color color)
        {
            var regex = new Regex(pattern);
            var matches = regex.Matches(lineText);

            foreach (Match match in matches)
            {
                if (match.Success)
                {
                    // Color the entire match (including markers)
                    int startOffset = line.Offset + match.Index;
                    int endOffset = startOffset + match.Length;

                    ChangeLinePart(startOffset, endOffset, element =>
                    {
                        element.TextRunProperties.SetForegroundBrush(new SolidColorBrush(color));
                    });
                }
            }
        }
    }
}