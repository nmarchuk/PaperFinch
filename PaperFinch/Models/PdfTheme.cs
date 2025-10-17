using PaperFinch.Models;

namespace PaperFinch.Models
{
    public class PdfTheme
    {
        public string Name { get; set; } = "Default";
        public int FontSize { get; set; } = 12;
        public double MarginSize { get; set; } = 1.0;
        public TrimSize TrimSize { get; set; } = TrimSize.Standard_6x9;

        public PdfTheme Clone()
        {
            return new PdfTheme
            {
                Name = Name,
                FontSize = FontSize,
                MarginSize = MarginSize,
                TrimSize = TrimSize
            };
        }
    }
}