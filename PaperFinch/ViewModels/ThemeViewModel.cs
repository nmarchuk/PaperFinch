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
        private double _lineSpacing = 1.6;
        private double _paragraphIndent = 0.3;
        private double _chapterHeadingTopOffset = 2.0;
        private double _chapterTitleBottomSpacing = 0.2;
        private double _chapterSubtitleBottomSpacing = 0.5;

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

        private HeaderContentType _leftPageHeaderContent = HeaderContentType.Author;
        private bool _leftPageHeaderCapitalize = true;
        private HeaderContentType _rightPageHeaderContent = HeaderContentType.Title;
        private bool _rightPageHeaderCapitalize = true;
        private PageNumberPositionItem _selectedPageNumberPosition = new PageNumberPositionItem { Position = PageNumberPosition.Top, DisplayName = "Top" };
        private bool _showSubtitlesInTOC = false;

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
        public double ChapterHeadingTopOffset
        {
            get => _chapterHeadingTopOffset;
            set => SetProperty(ref _chapterHeadingTopOffset, value);
        }

        public double ChapterTitleBottomSpacing
        {
            get => _chapterTitleBottomSpacing;
            set => SetProperty(ref _chapterTitleBottomSpacing, value);
        }

        public double ChapterSubtitleBottomSpacing
        {
            get => _chapterSubtitleBottomSpacing;
            set => SetProperty(ref _chapterSubtitleBottomSpacing, value);
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

        public HeaderContentType LeftPageHeaderContent
        {
            get => _leftPageHeaderContent;
            set => SetProperty(ref _leftPageHeaderContent, value);
        }

        public bool LeftPageHeaderCapitalize
        {
            get => _leftPageHeaderCapitalize;
            set => SetProperty(ref _leftPageHeaderCapitalize, value);
        }

        public HeaderContentType RightPageHeaderContent
        {
            get => _rightPageHeaderContent;
            set => SetProperty(ref _rightPageHeaderContent, value);
        }

        public bool RightPageHeaderCapitalize
        {
            get => _rightPageHeaderCapitalize;
            set => SetProperty(ref _rightPageHeaderCapitalize, value);
        }

        public PageNumberPositionItem SelectedPageNumberPosition
        {
            get => _selectedPageNumberPosition;
            set => SetProperty(ref _selectedPageNumberPosition, value);
        }

        public bool ShowSubtitlesInTOC
        {
            get => _showSubtitlesInTOC;
            set => SetProperty(ref _showSubtitlesInTOC, value);
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
                ChapterHeadingTopOffset = ChapterHeadingTopOffset,
                ChapterTitleBottomSpacing = ChapterTitleBottomSpacing,
                ChapterSubtitleBottomSpacing = ChapterSubtitleBottomSpacing,
                ChapterTitleFont = ChapterTitleFont,
                ChapterTitleFontSize = ChapterTitleFontSize,
                ChapterTitleBold = ChapterTitleBold,
                ChapterTitleItalic = ChapterTitleItalic,
                ChapterTitleAlignment = ChapterTitleAlignment,
                ChapterSubtitleFont = ChapterSubtitleFont,
                ChapterSubtitleFontSize = ChapterSubtitleFontSize,
                ChapterSubtitleBold = ChapterSubtitleBold,
                ChapterSubtitleItalic = ChapterSubtitleItalic,
                ChapterSubtitleAlignment = ChapterSubtitleAlignment,
                LeftPageHeaderContent = LeftPageHeaderContent,
                LeftPageHeaderCapitalize = LeftPageHeaderCapitalize,
                RightPageHeaderContent = RightPageHeaderContent,
                RightPageHeaderCapitalize = RightPageHeaderCapitalize,
                PageNumberPosition = SelectedPageNumberPosition?.Position ?? PageNumberPosition.Top,
                ShowSubtitlesInTOC = ShowSubtitlesInTOC
            };
        }

        // Apply a PdfTheme into this view model. If a TrimSizeItem/PageNumberPositionItem collection is provided,
        // try to select the matching item; otherwise create a simple item.
        public void ApplyFrom(PdfTheme theme, IEnumerable<TrimSizeItem>? availableTrimSizes = null, IEnumerable<PageNumberPositionItem>? availablePageNumberPositions = null)
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
            ChapterHeadingTopOffset = theme.ChapterHeadingTopOffset;
            ChapterTitleBottomSpacing = theme.ChapterTitleBottomSpacing;
            ChapterSubtitleBottomSpacing = theme.ChapterSubtitleBottomSpacing;
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
            LeftPageHeaderContent = theme.LeftPageHeaderContent;
            LeftPageHeaderCapitalize = theme.LeftPageHeaderCapitalize;
            RightPageHeaderContent = theme.RightPageHeaderContent;
            RightPageHeaderCapitalize = theme.RightPageHeaderCapitalize;
            ShowSubtitlesInTOC = theme.ShowSubtitlesInTOC;

            if (availablePageNumberPositions != null)
            {
                var match = availablePageNumberPositions.FirstOrDefault(p => p.Position == theme.PageNumberPosition);
                if (match != null) SelectedPageNumberPosition = match;
                else SelectedPageNumberPosition = new PageNumberPositionItem { Position = theme.PageNumberPosition, DisplayName = theme.PageNumberPosition.GetDescription() };
            }
            else
            {
                SelectedPageNumberPosition = new PageNumberPositionItem { Position = theme.PageNumberPosition, DisplayName = theme.PageNumberPosition.GetDescription() };
            }

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
