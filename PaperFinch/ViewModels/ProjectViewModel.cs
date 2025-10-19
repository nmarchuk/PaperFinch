using CommunityToolkit.Mvvm.ComponentModel;
using PaperFinch.Models;
using PaperFinch.Services;
using System.Threading.Tasks;

namespace PaperFinch.ViewModels
{
    public class ProjectViewModel : ObservableObject
    {
        private readonly ProjectService _projectService = new ProjectService();
        private string _bookTitle = "Untitled Book";
        private string _bookSubtitle = string.Empty;
        private string _bookAuthor = "Unknown Author";
        private string _bookPublisherName = string.Empty;
        private string _bookPublisherLink = string.Empty;

        public string BookTitle
        {
            get => _bookTitle;
            set => SetProperty(ref _bookTitle, value);
        }

        public string BookSubtitle
        {
            get => _bookSubtitle;
            set => SetProperty(ref _bookSubtitle, value);
        }

        public string BookAuthor
        {
            get => _bookAuthor;
            set => SetProperty(ref _bookAuthor, value);
        }

        public string BookPublisherName
        {
            get => _bookPublisherName;
            set => SetProperty(ref _bookPublisherName, value);
        }

        public string BookPublisherLink
        {
            get => _bookPublisherLink;
            set => SetProperty(ref _bookPublisherLink, value);
        }

        // Apply values from a loaded Project model into this VM
        public void ApplyFromModel(Project project)
        {
            if (project == null) return;
            BookTitle = string.IsNullOrWhiteSpace(project.BookTitle) ? BookTitle : project.BookTitle;
            BookSubtitle = project.BookSubtitle ?? BookSubtitle;
            BookAuthor = string.IsNullOrWhiteSpace(project.BookAuthor) ? BookAuthor : project.BookAuthor;
            BookPublisherName = project.BookPublisherName ?? BookPublisherName;
            BookPublisherLink = project.BookPublisherLink ?? BookPublisherLink;
        }

        // Build a Project model from current VM state plus caller-supplied pieces
        public Project ToModel(
            string name,
            PdfTheme theme,
            string chapterTitle,
            string chapterSubtitle,
            string content)
        {
            return _projectService.CreateProjectFromState(
                name ?? BookTitle,
                theme,
                BookTitle,
                BookSubtitle,
                BookAuthor,
                BookPublisherName,
                BookPublisherLink,
                chapterTitle,
                chapterSubtitle,
                content);
        }

        // Save helpers that encapsulate ProjectService usage so callers stay compact.
        public Task SaveToFileAsync(PdfTheme theme, string chapterTitle, string chapterSubtitle, string content, string? name = null)
        {
            var project = ToModel(name ?? BookTitle, theme, chapterTitle, chapterSubtitle, content);
            return _projectService.SaveProjectAsync(project);
        }

        public async Task<Project> LoadFromFileAsync(string projectName)
        {
            var project = await _projectService.LoadProjectAsync(projectName);
            if (project != null)
            {
                ApplyFromModel(project);
            }
            return project;
        }

        public Task DeleteFileAsync(string projectName) => _projectService.DeleteProjectAsync(projectName);
    }
}