using CommunityToolkit.Mvvm.ComponentModel;
using PaperFinch.Models;
using PaperFinch.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
        private ChapterViewModel? _selectedChapter;

        // Action to notify MainWindowViewModel when chapter selection changes
        public Action? OnChapterSelectionChanged { get; set; }

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

        public ObservableCollection<ChapterViewModel> Chapters { get; } = new ObservableCollection<ChapterViewModel>();

        public ChapterViewModel? SelectedChapter
        {
            get => _selectedChapter;
            set
            {
                if (SetProperty(ref _selectedChapter, value))
                {
                    OnChapterSelectionChanged?.Invoke();
                }
            }
        }

        public ProjectViewModel()
        {
            // Initialize with one default chapter
            var defaultChapter = new ChapterViewModel
            {
                Title = "Chapter 1",
                Subtitle = string.Empty,
                Content = string.Empty
            };
            Chapters.Add(defaultChapter);
            SelectedChapter = defaultChapter;
        }

        public void ApplyFromModel(Project project)
        {
            if (project == null) return;

            BookTitle = string.IsNullOrWhiteSpace(project.BookTitle) ? BookTitle : project.BookTitle;
            BookSubtitle = project.BookSubtitle ?? BookSubtitle;
            BookAuthor = string.IsNullOrWhiteSpace(project.BookAuthor) ? BookAuthor : project.BookAuthor;
            BookPublisherName = project.BookPublisherName ?? BookPublisherName;
            BookPublisherLink = project.BookPublisherLink ?? BookPublisherLink;

            // Load chapters
            Chapters.Clear();
            foreach (var chapter in project.Chapters)
            {
                Chapters.Add(new ChapterViewModel
                {
                    Title = chapter.Title,
                    Subtitle = chapter.Subtitle,
                    Content = chapter.Content ?? string.Empty,
                    ExcludeFromPageCount = chapter.ExcludeFromPageCount,
                    IsTableOfContents = chapter.IsTableOfContents,
                    NoIndent = chapter.NoIndent
                });
            }

            // Select first chapter if available
            SelectedChapter = Chapters.FirstOrDefault();
        }

        public Project ToModel(string name, PdfTheme theme)
        {
            var chapters = Chapters.Select(c => new Chapter
            {
                Title = c.Title,
                Subtitle = c.Subtitle,
                Content = c.Content,
                ExcludeFromPageCount = c.ExcludeFromPageCount,
                IsTableOfContents = c.IsTableOfContents,
                NoIndent = c.NoIndent
            }).ToList();

            var project = _projectService.CreateProjectFromChapters(
                name ?? BookTitle,
                theme,
                BookTitle,
                BookSubtitle,
                BookAuthor,
                BookPublisherName,
                BookPublisherLink,
                chapters);

            return project;
        }

        public Task SaveToFileAsync(PdfTheme theme, string? name = null)
        {
            var project = ToModel(name ?? BookTitle, theme);
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

    public class ChapterViewModel : ObservableObject
    {
        private string _title = string.Empty;
        private string _subtitle = string.Empty;
        private string _content = string.Empty;
        private bool _excludeFromPageCount = false;
        private bool _isTableOfContents = false;
        private bool _noIndent = false;

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Subtitle
        {
            get => _subtitle;
            set => SetProperty(ref _subtitle, value);
        }

        public string Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        public bool ExcludeFromPageCount
        {
            get => _excludeFromPageCount;
            set => SetProperty(ref _excludeFromPageCount, value);
        }

        public bool IsTableOfContents
        {
            get => _isTableOfContents;
            set => SetProperty(ref _isTableOfContents, value);
        }

        public bool NoIndent
        {
            get => _noIndent;
            set => SetProperty(ref _noIndent, value);
        }
    }
}