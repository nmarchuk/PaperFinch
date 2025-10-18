using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PaperFinch.Services
{
    public class FontService
    {
        private static FontService? _instance;
        private readonly List<string> _availableFonts;

        public static FontService Instance => _instance ??= new FontService();

        private FontService()
        {
            _availableFonts = new List<string>();

            // Load custom fonts from a Fonts directory if it exists
            var fontsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts");
            if (Directory.Exists(fontsDirectory))
            {
                LoadCustomFonts(fontsDirectory);
            }

            // If no custom fonts found, add some common system fonts as defaults
            if (_availableFonts.Count == 0)
            {
                _availableFonts.AddRange(new[]
                {
                    "Times New Roman",
                    "Arial",
                    "Courier New",
                    "Georgia",
                    "Verdana",
                    "Calibri"
                });
            }
        }

        public List<string> GetAvailableFonts() => new List<string>(_availableFonts);

        private void LoadCustomFonts(string fontsDirectory)
        {
            try
            {
                var fontFiles = Directory.GetFiles(fontsDirectory, "*.ttf")
                    .Concat(Directory.GetFiles(fontsDirectory, "*.otf"))
                    .ToList();

                foreach (var fontFile in fontFiles)
                {
                    try
                    {
                        // Register the font with QuestPDF
                        FontManager.RegisterFont(File.OpenRead(fontFile));

                        // Extract font name from file name (without extension)
                        var fontName = Path.GetFileNameWithoutExtension(fontFile);
                        _availableFonts.Add(fontName);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading font {fontFile}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading custom fonts: {ex.Message}");
            }
        }
    }
}