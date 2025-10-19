using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PaperFinch.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

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
}