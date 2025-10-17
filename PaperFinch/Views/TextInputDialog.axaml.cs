using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PaperFinch.Views
{
    public partial class TextInputDialog : Window
    {
        public string Message
        {
            get => MessageTextBlock.Text ?? string.Empty;
            set => MessageTextBlock.Text = value;
        }

        public string InputText
        {
            get => InputTextBox.Text ?? string.Empty;
            set => InputTextBox.Text = value;
        }

        public TextInputDialog()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(InputText);
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }
    }
}