using System;
using System.Collections.Generic;

namespace PaperFinch.Models
{
    public class Project
    {
        // Optional friendly name for the project file
        public string? Name { get; set; }

        // Theme (serialized as part of the project)
        public PdfTheme Theme { get; set; } = new PdfTheme();

        // Metadata
        public string BookTitle { get; set; } = "Untitled Book";
        public string BookSubtitle { get; set; } = string.Empty;
        public string BookAuthor { get; set; } = "Unknown Author";
        public string BookPublisherName { get; set; } = string.Empty;
        public string BookPublisherLink { get; set; } = string.Empty;

        // Multi-chapter support
        public List<Chapter> Chapters { get; set; } = new List<Chapter>();

        // Timestamps
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
    }

    public class Chapter
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty; // Now stores Markdown
        public bool ExcludeFromPageCount { get; set; } = false;
        public bool IsTableOfContents { get; set; } = false;
        public bool NoIndent { get; set; } = false;
    }
}