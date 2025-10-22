using PaperFinch.Models;
using QuestPDF.Infrastructure;
using System.ComponentModel;
using System.Linq;

namespace PaperFinch.Models
{
    public enum HeaderContentType
    {
        None,
        Title,
        Author,
        ChapterTitle
    }

    public enum PageNumberPosition
    {
        Top,
        Bottom,
        [Description("Bottom Centered")]
        BottomCentered
    }

    public static class PageNumberPositionExtensions
    {
        public static string GetDescription(this PageNumberPosition position)
        {
            var field = position.GetType().GetField(position.ToString());
            var attribute = (DescriptionAttribute?)field?.GetCustomAttributes(typeof(DescriptionAttribute), false).FirstOrDefault();
            return attribute?.Description ?? position.ToString();
        }
    }

    public class PdfTheme
    {
        public string Name { get; set; } = "Default";

        // Page Settings
        public TrimSize TrimSize { get; set; } = TrimSize.Standard_6x9;
        public double InsideMargin { get; set; } = 0.875;
        public double OutsideMargin { get; set; } = 0.5;
        public double TopMargin { get; set; } = 0.75;
        public double BottomMargin { get; set; } = 0.75;

        // Body Text Settings
        public string BodyFont { get; set; } = "Times New Roman";
        public int BodyFontSize { get; set; } = 12;
        public double LineSpacing { get; set; } = 1.6;
        public double ParagraphIndent { get; set; } = 0.3;
        public double ChapterHeadingTopOffset { get; set; } = 2.0; // in inches, about 1/3 down a typical page
        public double ChapterTitleBottomSpacing { get; set; } = 0.2; // in inches
        public double ChapterSubtitleBottomSpacing { get; set; } = 0.5; // in inches

        // Chapter Title Settings
        public string ChapterTitleFont { get; set; } = "Times New Roman";
        public int ChapterTitleFontSize { get; set; } = 24;
        public bool ChapterTitleBold { get; set; } = true;
        public bool ChapterTitleItalic { get; set; } = false;
        public TextAlignment ChapterTitleAlignment { get; set; } = TextAlignment.Center;

        // Chapter Subtitle Settings
        public string ChapterSubtitleFont { get; set; } = "Times New Roman";
        public int ChapterSubtitleFontSize { get; set; } = 18;
        public bool ChapterSubtitleBold { get; set; } = false;
        public bool ChapterSubtitleItalic { get; set; } = true;
        public TextAlignment ChapterSubtitleAlignment { get; set; } = TextAlignment.Center;

        // Header Settings (running headers)
        public HeaderContentType LeftPageHeaderContent { get; set; } = HeaderContentType.Author;
        public bool LeftPageHeaderCapitalize { get; set; } = true;
        public HeaderContentType RightPageHeaderContent { get; set; } = HeaderContentType.Title;
        public bool RightPageHeaderCapitalize { get; set; } = true;

        // Page Number Settings
        public PageNumberPosition PageNumberPosition { get; set; } = PageNumberPosition.Top;

        // Misc. Settings
        public bool ShowSubtitlesInTOC { get; set; } = false;

        public PdfTheme Clone()
        {
            return new PdfTheme
            {
                Name = Name,
                TrimSize = TrimSize,
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
                PageNumberPosition = PageNumberPosition,
                ShowSubtitlesInTOC = ShowSubtitlesInTOC
            };
        }
    }

    public enum TextAlignment
    {
        Left,
        Center,
        Right,
        Justify
    }
}