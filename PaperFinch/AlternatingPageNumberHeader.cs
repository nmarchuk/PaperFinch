using PaperFinch.Models;
using QuestPDF.Elements;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PaperFinch.Components
{
    public struct HeaderState
    {
        public int PagesRendered { get; set; }
    }

    public class AlternatingPageNumberHeader : IDynamicComponent<HeaderState>
    {
        private readonly int _startingPageNumber;
        private readonly PdfTheme _theme;

        public AlternatingPageNumberHeader(int startingPageNumber, PdfTheme theme)
        {
            _startingPageNumber = startingPageNumber;
            _theme = theme;
        }

        public HeaderState State { get; set; } = new HeaderState { PagesRendered = 0 };

        public DynamicComponentComposeResult Compose(DynamicContext context)
        {
            var state = State;

            // Calculate the actual page number to display
            int pageNumber = _startingPageNumber + state.PagesRendered;

            // Determine if this is an odd or even page (for alternating margins)
            // Page 1 is typically odd (right page), page 2 is even (left page)
            bool isOddPage = pageNumber % 2 == 0;

            var content = context.CreateElement(element =>
            {
                float leftMargin = isOddPage ? (float)_theme.OutsideMargin : (float)_theme.InsideMargin;
                float rightMargin = isOddPage ? (float)_theme.InsideMargin : (float)_theme.OutsideMargin;

                element
                    .PaddingLeft(leftMargin, Unit.Inch)
                    .PaddingRight(rightMargin, Unit.Inch)
                    .PaddingTop(0.25f, Unit.Inch)
                    .PaddingBottom(0.25f, Unit.Inch)
                    .Row(row =>
                    {
                        if (isOddPage)
                        {
                            // Odd pages: page number on the right
                            row.RelativeItem().Text("");
                            row.ConstantItem(50).AlignRight().Text(pageNumber.ToString())
                                .FontFamily(_theme.BodyFont)
                                .FontSize(_theme.BodyFontSize);
                        }
                        else
                        {
                            // Even pages: page number on the left
                            row.ConstantItem(50).AlignLeft().Text(pageNumber.ToString())
                                .FontFamily(_theme.BodyFont)
                                .FontSize(_theme.BodyFontSize);
                            row.RelativeItem().Text("");
                        }
                    });
            });

            // Increment state for next page
            state.PagesRendered++;
            State = state;

            return new DynamicComponentComposeResult
            {
                Content = content,
                HasMoreContent = true // Keep rendering for each page
            };
        }
    }
}