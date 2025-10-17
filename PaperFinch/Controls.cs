using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PdfiumViewer;
using System;
using System.IO;

namespace PaperFinch.Controls
{
    /// <summary>
    /// Simple PDF preview control that renders PDF bytes to an image
    /// </summary>
    public class PdfPreviewControl : UserControl
    {
        private Image _imageControl;
        private byte[] _pdfData;

        public PdfPreviewControl()
        {
            _imageControl = new Image
            {
                Stretch = Avalonia.Media.Stretch.Uniform
            };
            Content = _imageControl;
        }

        /// <summary>
        /// Load and render a PDF from a byte array
        /// </summary>
        public void LoadPdf(byte[] pdfBytes, int pageIndex = 0, int dpi = 150)
        {
            if (pdfBytes == null || pdfBytes.Length == 0)
            {
                _imageControl.Source = null;
                return;
            }

            _pdfData = pdfBytes;
            RenderPage(pageIndex, dpi);
        }

        /// <summary>
        /// Render a specific page of the loaded PDF
        /// </summary>
        private void RenderPage(int pageIndex, int dpi)
        {
            try
            {
                using var stream = new MemoryStream(_pdfData);
                using var pdfDocument = PdfDocument.Load(stream);

                if (pageIndex < 0 || pageIndex >= pdfDocument.PageCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(pageIndex),
                        $"Page index {pageIndex} is out of range. Document has {pdfDocument.PageCount} pages.");
                }

                // Get page size
                var pageSize = pdfDocument.PageSizes[pageIndex];

                // Calculate pixel dimensions based on DPI
                double scale = dpi / 72.0;
                int width = (int)(pageSize.Width * scale);
                int height = (int)(pageSize.Height * scale);

                // Render page to System.Drawing.Image
                using var image = pdfDocument.Render(pageIndex, width, height, dpi, dpi, false);

                // Convert to Avalonia bitmap
                var avaloniaBitmap = ConvertToAvaloniaBitmap(image);
                _imageControl.Source = avaloniaBitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error rendering PDF: {ex.Message}");
                _imageControl.Source = null;
            }
        }

        /// <summary>
        /// Convert System.Drawing.Image to Avalonia Bitmap
        /// </summary>
        private Bitmap ConvertToAvaloniaBitmap(System.Drawing.Image drawingImage)
        {
            using var ms = new MemoryStream();

            // Save as PNG to memory stream
            drawingImage.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;

            // Load into Avalonia Bitmap
            return new Bitmap(ms);
        }

        /// <summary>
        /// Get the number of pages in the currently loaded PDF
        /// </summary>
        public int GetPageCount()
        {
            if (_pdfData == null || _pdfData.Length == 0)
                return 0;

            try
            {
                using var stream = new MemoryStream(_pdfData);
                using var pdfDocument = PdfDocument.Load(stream);
                return pdfDocument.PageCount;
            }
            catch
            {
                return 0;
            }
        }
    }
}