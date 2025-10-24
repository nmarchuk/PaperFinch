using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PaperFinch.Views
{
    public enum SaveBeforeCloseResult
    {
        Cancel,
        SaveAndQuit,
        QuitWithoutSaving
    }

    public partial class SaveBeforeCloseDialog : Window
    {
        public string Message
        {
            get => MessageTextBlock.Text ?? string.Empty;
            set => MessageTextBlock.Text = value;
        }

        public SaveBeforeCloseDialog()
        {
            InitializeComponent();
        }

        private void SaveAndQuitButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(SaveBeforeCloseResult.SaveAndQuit);
        }

        private void QuitWithoutSavingButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(SaveBeforeCloseResult.QuitWithoutSaving);
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(SaveBeforeCloseResult.Cancel);
        }
    }
}
