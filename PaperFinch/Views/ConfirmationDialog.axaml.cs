using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PaperFinch.Views
{
    public partial class ConfirmationDialog : Window
    {
        public string Message
        {
            get => MessageTextBlock.Text ?? string.Empty;
            set => MessageTextBlock.Text = value;
        }

        public ConfirmationDialog()
        {
            InitializeComponent();
        }

        private void YesButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(true);
        }

        private void NoButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
