using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PaperFinch.Models;

namespace PaperFinch.Services
{
    public class ThemeService
    {
        private readonly string _themesDirectory;
        private readonly JsonSerializerOptions _jsonOptions;

        public ThemeService()
        {
            // Store themes in the app's data directory
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _themesDirectory = Path.Combine(appDataPath, "PaperFinch", "Themes");

            // Ensure directory exists
            Directory.CreateDirectory(_themesDirectory);

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<List<PdfTheme>> LoadAllThemesAsync()
        {
            var themes = new List<PdfTheme>();

            // Add default theme
            themes.Add(new PdfTheme
            {
                Name = "Default",
                FontSize = 12,
                MarginSize = 1.0,
                TrimSize = TrimSize.Standard_6x9
            });

            // Load all theme files
            var themeFiles = Directory.GetFiles(_themesDirectory, "*.json");
            foreach (var file in themeFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var theme = JsonSerializer.Deserialize<PdfTheme>(json, _jsonOptions);
                    if (theme != null)
                    {
                        themes.Add(theme);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading theme {file}: {ex.Message}");
                }
            }

            return themes;
        }

        public async Task SaveThemeAsync(PdfTheme theme)
        {
            var fileName = SanitizeFileName(theme.Name) + ".json";
            var filePath = Path.Combine(_themesDirectory, fileName);

            var json = JsonSerializer.Serialize(theme, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
        }

        public async Task DeleteThemeAsync(string themeName)
        {
            if (themeName == "Default")
                return; // Can't delete default theme

            var fileName = SanitizeFileName(themeName) + ".json";
            var filePath = Path.Combine(_themesDirectory, fileName);

            if (File.Exists(filePath))
            {
                await Task.Run(() => File.Delete(filePath));
            }
        }

        public async Task<bool> ThemeExistsAsync(string themeName)
        {
            if (themeName == "Default")
                return true;

            var fileName = SanitizeFileName(themeName) + ".json";
            var filePath = Path.Combine(_themesDirectory, fileName);
            return await Task.Run(() => File.Exists(filePath));
        }

        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        }
    }
}