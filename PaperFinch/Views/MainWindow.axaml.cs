using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
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

        // Handle window closing to prompt for save
        this.Closing += OnWindowClosing;
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

            // Optional: Auto-generate on startup (with a small delay to let UI settle)
            Dispatcher.UIThread.Post(() => vm.GeneratePdfCommand.Execute(null));
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

        // Add keyboard shortcuts for markdown formatting
        SetupKeyboardShortcuts();
    }

    private void SetupKeyboardShortcuts()
    {
        // Use tunnel routing strategy to intercept keys before they're consumed by AvaloniaEdit
        // See: https://github.com/AvaloniaUI/AvaloniaEdit/issues/383
        ContentEditor.AddHandler(KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        // Check for Ctrl (Windows/Linux) or Cmd (Mac)
        bool isModifierPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control) ||
                                 e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        if (!isModifierPressed)
            return;

        switch (e.Key)
        {
            case Key.B:
                InsertMarkdownFormatting("**", "**");
                e.Handled = true;
                break;
            case Key.I:
                InsertMarkdownFormatting("*", "*");
                e.Handled = true;
                break;
            case Key.U:
                InsertMarkdownFormatting("++", "++");
                e.Handled = true;
                break;
        }
    }

    private void InsertMarkdownFormatting(string prefix, string suffix)
    {
        if (ContentEditor.Document == null)
            return;

        var textArea = ContentEditor.TextArea;
        var selection = textArea.Selection;
        var document = ContentEditor.Document;

        if (!selection.IsEmpty)
        {
            // Wrap selected text
            var selectedText = selection.GetText();
            var startOffset = selection.SurroundingSegment.Offset;
            var length = selection.SurroundingSegment.Length;

            document.Replace(startOffset, length, prefix + selectedText + suffix);

            // Update selection to highlight the newly formatted text (excluding markers)
            textArea.Selection = Selection.Create(textArea, startOffset + prefix.Length,
                                                   startOffset + prefix.Length + selectedText.Length);
            textArea.Caret.Offset = startOffset + prefix.Length + selectedText.Length;
        }
        else
        {
            // Insert markers at cursor position
            var caretOffset = textArea.Caret.Offset;
            document.Insert(caretOffset, prefix + suffix);

            // Move cursor between the markers
            textArea.Caret.Offset = caretOffset + prefix.Length;
        }

        textArea.Focus();
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

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        // Cancel the close event initially so we can show the dialog
        e.Cancel = true;

        if (DataContext is not MainWindowViewModel vm)
        {
            // If no ViewModel, just close
            e.Cancel = false;
            return;
        }

        // Show the save confirmation dialog
        var dialog = new SaveBeforeCloseDialog();
        var result = await dialog.ShowDialog<SaveBeforeCloseResult>(this);

        switch (result)
        {
            case SaveBeforeCloseResult.SaveAndQuit:
                // Save theme first (if not default)
                if (vm.SelectedTheme != null && vm.SelectedTheme.Name != "Default")
                {
                    await vm.SaveThemeCommand.ExecuteAsync(null);
                }

                // Save project (if not default)
                if (vm.SelectedProject != null && vm.SelectedProject.Name != "Default")
                {
                    await vm.SaveProjectCommand.ExecuteAsync(null);
                }

                // Now close the window
                this.Closing -= OnWindowClosing; // Remove handler to avoid recursion
                Close();
                break;

            case SaveBeforeCloseResult.QuitWithoutSaving:
                // Close without saving
                this.Closing -= OnWindowClosing; // Remove handler to avoid recursion
                Close();
                break;

            case SaveBeforeCloseResult.Cancel:
                // Do nothing, window stays open (e.Cancel is already true)
                break;
        }
    }
}