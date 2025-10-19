using PaperFinch.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PaperFinch.Services
{
    public class ProjectService
    {
        private readonly string _projectsDirectory;
        private readonly JsonSerializerOptions _jsonOptions;

        public ProjectService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _projectsDirectory = Path.Combine(appDataPath, "PaperFinch", "Projects");

            // Ensure directory exists
            Directory.CreateDirectory(_projectsDirectory);

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
        }

        public async Task<List<Project>> LoadAllProjectsAsync()
        {
            var projects = new List<Project>();
            var projectFiles = Directory.GetFiles(_projectsDirectory, "*.pfproj");

            foreach (var file in projectFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var project = JsonSerializer.Deserialize<Project>(json, _jsonOptions);
                    if (project != null)
                    {
                        projects.Add(project);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading project '{file}': {ex.Message}");
                }
            }

            return projects.OrderByDescending(p => p.ModifiedUtc).ToList();
        }

        public async Task<Project?> LoadProjectAsync(string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName))
                return null;

            var fileName = SanitizeFileName(projectName) + ".pfproj";
            var filePath = Path.Combine(_projectsDirectory, fileName);

            if (!File.Exists(filePath))
                return null;

            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<Project>(json, _jsonOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading project '{projectName}': {ex.Message}");
                return null;
            }
        }

        public async Task SaveProjectAsync(Project project)
        {
            if (project == null)
                throw new ArgumentNullException(nameof(project));

            project.ModifiedUtc = DateTime.UtcNow;
            if (project.CreatedUtc == default)
                project.CreatedUtc = project.ModifiedUtc;

            var fileName = SanitizeFileName(project.Name) + ".pfproj";
            var filePath = Path.Combine(_projectsDirectory, fileName);

            var json = JsonSerializer.Serialize(project, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
        }

        public async Task DeleteProjectAsync(string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName))
                return;

            var fileName = SanitizeFileName(projectName) + ".pfproj";
            var filePath = Path.Combine(_projectsDirectory, fileName);

            if (File.Exists(filePath))
                await Task.Run(() => File.Delete(filePath));
        }

        public async Task<bool> ProjectExistsAsync(string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName))
                return false;

            var fileName = SanitizeFileName(projectName) + ".pfproj";
            var filePath = Path.Combine(_projectsDirectory, fileName);
            return await Task.Run(() => File.Exists(filePath));
        }

        public string GetProjectsDirectory() => _projectsDirectory;

        private string SanitizeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("_", name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        }

        public Project CreateProjectFromState(string name, PdfTheme theme, string bookTitle, string bookSubtitle, string bookAuthor, string bookPublisherName, string bookPublisherLink, string chapterTitle, string chapterSubtitle, string content)
        {
            var project = new Project
            {
                Name = name,
                Theme = theme ?? new PdfTheme(),
                BookTitle = string.IsNullOrWhiteSpace(bookTitle) ? "Untitled Book" : bookTitle,
                BookSubtitle = bookSubtitle ?? string.Empty,
                BookAuthor = string.IsNullOrWhiteSpace(bookAuthor) ? "Unknown Author" : bookAuthor,
                BookPublisherName = bookPublisherName ?? string.Empty,
                BookPublisherLink = bookPublisherLink ?? string.Empty,
                ChapterTitle = chapterTitle ?? string.Empty,
                ChapterSubtitle = chapterSubtitle ?? string.Empty,
                Content = content ?? string.Empty,
                CreatedUtc = DateTime.UtcNow,
                ModifiedUtc = DateTime.UtcNow
            };
            return project;
        }
    }
}