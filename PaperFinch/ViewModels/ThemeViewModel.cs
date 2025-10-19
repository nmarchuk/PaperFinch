using CommunityToolkit.Mvvm.ComponentModel;
using PaperFinch.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PaperFinch.ViewModels
{
    public class ThemeViewModel : ObservableObject
    {
        private double _insideMargin = 0.875;
        private double _outsideMargin = 0.5;
        private double _topMargin = 0.75;
        private double _bottomMargin = 0.75;

        private TrimSizeItem _selectedTrimSize = new TrimSizeItem { Size = TrimSize.Standard_6x9, DisplayName = TrimSize.Standard_6x9.GetDescription() };

        private string _bodyFont = "Times New Roman";
        private int _bodyFontSize = 12;
        private double _lineSpacing = 1.2;
        private double _paragraphIndent = 0.3;

        private string _chapterTitleFont = "Times New Roman";
        private int _chapterTitleFontSize = 24;
        private bool _chapterTitleBold = true;
        private bool _chapterTitleItalic = false;
        private TextAlignment _chapterTitleAlignment = TextAlignment.Center;

        private string _chapterSubtitleFont = "Times New Roman";
        private int _chapterSubtitleFontSize = 18;
        private bool _chapterSubtitleBold = false;
        private bool _chapterSubtitleItalic = true;
        private TextAlignment _chapterSubtitleAlignment = TextAlignment.Center;

        public double InsideMargin
        {
            get => _insideMargin;
            set => SetProperty(ref _insideMargin, value);
        }

        public double OutsideMargin
        {
            get => _outsideMargin;
            set => SetProperty(ref _outsideMargin, value);
        }

        public double TopMargin
        {
            get => _topMargin;
            set => SetProperty(ref _topMargin, value);
        }

        public double BottomMargin
        {
            get => _bottomMargin;
            set => SetProperty(ref _bottomMargin, value);
        }

        // Trim size as selected item (keeps compatibility with existing TrimSizeItem usage)
        public TrimSizeItem SelectedTrimSize
        {
            get => _selectedTrimSize;
            set => SetProperty(ref _selectedTrimSize, value);
        }

        public string BodyFont
        {
            get => _bodyFont;
            set => SetProperty(ref _bodyFont, value);
        }

        public int BodyFontSize
        {
            get => _bodyFontSize;
            set => SetProperty(ref _bodyFontSize, value);
        }

        public double LineSpacing
        {
            get => _lineSpacing;
            set => SetProperty(ref _lineSpacing, value);
        }

        public double ParagraphIndent
        {
            get => _paragraphIndent;
            set => SetProperty(ref _paragraphIndent, value);
        }

        public string ChapterTitleFont
        {
            get => _chapterTitleFont;
            set => SetProperty(ref _chapterTitleFont, value);
        }

        public int ChapterTitleFontSize
        {
            get => _chapterTitleFontSize;
            set => SetProperty(ref _chapterTitleFontSize, value);
        }

        public bool ChapterTitleBold
        {
            get => _chapterTitleBold;
            set => SetProperty(ref _chapterTitleBold, value);
        }

        public bool ChapterTitleItalic
        {
            get => _chapterTitleItalic;
            set => SetProperty(ref _chapterTitleItalic, value);
        }

        public TextAlignment ChapterTitleAlignment
        {
            get => _chapterTitleAlignment;
            set => SetProperty(ref _chapterTitleAlignment, value);
        }

        public string ChapterSubtitleFont
        {
            get => _chapterSubtitleFont;
            set => SetProperty(ref _chapterSubtitleFont, value);
        }

        public int ChapterSubtitleFontSize
        {
            get => _chapterSubtitleFontSize;
            set => SetProperty(ref _chapterSubtitleFontSize, value);
        }

        public bool ChapterSubtitleBold
        {
            get => _chapterSubtitleBold;
            set => SetProperty(ref _chapterSubtitleBold, value);
        }

        public bool ChapterSubtitleItalic
        {
            get => _chapterSubtitleItalic;
            set => SetProperty(ref _chapterSubtitleItalic, value);
        }

        public TextAlignment ChapterSubtitleAlignment
        {
            get => _chapterSubtitleAlignment;
            set => SetProperty(ref _chapterSubtitleAlignment, value);
        }

        // Create a PdfTheme instance from current ThemeViewModel values
        public PdfTheme ToPdfTheme(string name)
        {
            return new PdfTheme
            {
                Name = name ?? "Theme",
                TrimSize = SelectedTrimSize?.Size ?? TrimSize.Standard_6x9,
                InsideMargin = InsideMargin,
                OutsideMargin = OutsideMargin,
                TopMargin = TopMargin,
                BottomMargin = BottomMargin,
                BodyFont = BodyFont,
                BodyFontSize = BodyFontSize,
                LineSpacing = LineSpacing,
                ParagraphIndent = ParagraphIndent,
                ChapterTitleFont = ChapterTitleFont,
                ChapterTitleFontSize = ChapterTitleFontSize,
                ChapterTitleBold = ChapterTitleBold,
                ChapterTitleItalic = ChapterTitleItalic,
                ChapterTitleAlignment = ChapterTitleAlignment,
                ChapterSubtitleFont = ChapterSubtitleFont,
                ChapterSubtitleFontSize = ChapterSubtitleFontSize,
                ChapterSubtitleBold = ChapterSubtitleBold,
                ChapterSubtitleItalic = ChapterSubtitleItalic,
                ChapterSubtitleAlignment = ChapterSubtitleAlignment
            };
        }

        // Apply a PdfTheme into this view model. If a TrimSizeItem collection is provided,
        // try to select the matching TrimSizeItem; otherwise create a simple TrimSizeItem.
        public void ApplyFrom(PdfTheme theme, IEnumerable<TrimSizeItem>? availableTrimSizes = null)
        {
            if (theme == null) return;

            InsideMargin = theme.InsideMargin;
            OutsideMargin = theme.OutsideMargin;
            TopMargin = theme.TopMargin;
            BottomMargin = theme.BottomMargin;
            BodyFont = theme.BodyFont;
            BodyFontSize = theme.BodyFontSize;
            LineSpacing = theme.LineSpacing;
            ParagraphIndent = theme.ParagraphIndent;
            ChapterTitleFont = theme.ChapterTitleFont;
            ChapterTitleFontSize = theme.ChapterTitleFontSize;
            ChapterTitleBold = theme.ChapterTitleBold;
            ChapterTitleItalic = theme.ChapterTitleItalic;
            ChapterTitleAlignment = theme.ChapterTitleAlignment;
            ChapterSubtitleFont = theme.ChapterSubtitleFont;
            ChapterSubtitleFontSize = theme.ChapterSubtitleFontSize;
            ChapterSubtitleBold = theme.ChapterSubtitleBold;
            ChapterSubtitleItalic = theme.ChapterSubtitleItalic;
            ChapterSubtitleAlignment = theme.ChapterSubtitleAlignment;

            if (availableTrimSizes != null)
            {
                var match = availableTrimSizes.FirstOrDefault(t => t.Size == theme.TrimSize);
                if (match != null) SelectedTrimSize = match;
                else SelectedTrimSize = new TrimSizeItem { Size = theme.TrimSize, DisplayName = theme.TrimSize.GetDescription() };
            }
            else
            {
                SelectedTrimSize = new TrimSizeItem { Size = theme.TrimSize, DisplayName = theme.TrimSize.GetDescription() };
            }
        }
    }
}
