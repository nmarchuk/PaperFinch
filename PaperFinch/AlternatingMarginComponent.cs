using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using QuestPDF.Elements;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PaperFinch.Components
{
    public struct ContentState
    {
        public int ParagraphIndex { get; set; }
        public bool TitleRendered { get; set; }
        public bool SubtitleRendered { get; set; }
        public int CharacterOffset { get; set; }  // Track position within current paragraph
    }

    public class AlternatingMarginContent : IDynamicComponent<ContentState>
    {
        private readonly string _chapterTitle;
        private readonly string _chapterSubtitle;
        private readonly string _markdownContent;
        private readonly Models.PdfTheme _theme;
        private readonly int _pageNumberOffset;
        private readonly bool _showPageNumbers;
        private readonly string _bookTitle;
        private readonly string _bookAuthor;
        private readonly Action<int>? _onFirstPageCallback;
        private bool _firstPageReported = false;
        private readonly List<string> _paragraphs; // Plain text paragraphs parsed from Markdown

        public AlternatingMarginContent(string chapterTitle, string chapterSubtitle, string markdownContent, Models.PdfTheme theme, int pageNumberOffset = 0, bool showPageNumbers = false, string bookTitle = "", string bookAuthor = "", Action<int>? onFirstPage = null)
        {
            _chapterTitle = chapterTitle;
            _chapterSubtitle = chapterSubtitle;
            _markdownContent = markdownContent ?? string.Empty;
            _theme = theme;
            _pageNumberOffset = pageNumberOffset;
            _showPageNumbers = showPageNumbers;
            _bookTitle = bookTitle;
            _bookAuthor = bookAuthor;
            _onFirstPageCallback = onFirstPage;

            // Parse Markdown into paragraphs
            _paragraphs = ParseMarkdownToParagraphs(markdownContent ?? string.Empty);
        }

        private string StripMarkdownFormatting(string text)
        {
            // Remove Markdown formatting characters for measurement
            // This should match what gets rendered (text without markers)
            var stripped = text;

            // Remove bold markers: **text** or __text__
            stripped = Regex.Replace(stripped, @"\*\*(.+?)\*\*", "$1");
            stripped = Regex.Replace(stripped, @"__(.+?)__", "$1");

            // Remove italic markers: *text* or _text_ (but not if part of bold)
            stripped = Regex.Replace(stripped, @"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", "$1");
            stripped = Regex.Replace(stripped, @"(?<!_)_(?!_)(.+?)(?<!_)_(?!_)", "$1");

            // Remove underline markers: ++text++
            stripped = Regex.Replace(stripped, @"\+\+(.+?)\+\+", "$1");

            return stripped;
        }

        private List<string> ParseMarkdownToParagraphs(string markdown)
        {
            // Split by double newlines for proper paragraph separation
            // Single newlines within a paragraph are preserved for Markdown to handle
            if (string.IsNullOrWhiteSpace(markdown))
                return new List<string> { string.Empty };

            // Split on double newlines (with optional carriage returns)
            var paragraphs = markdown.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.None)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            // If no double newlines found but there's content, treat entire content as one paragraph
            if (paragraphs.Count == 0 && !string.IsNullOrWhiteSpace(markdown))
            {
                paragraphs.Add(markdown.Trim());
            }

            return paragraphs.Count > 0 ? paragraphs : new List<string> { string.Empty };
        }

        public ContentState State { get; set; } = new ContentState
        {
            ParagraphIndex = 0,
            TitleRendered = false,
            SubtitleRendered = false,
            CharacterOffset = 0
        };

        public DynamicComponentComposeResult Compose(DynamicContext context)
        {
            var state = State;

            // Report the first page number back to the caller (only once)
            if (!_firstPageReported && _onFirstPageCallback != null)
            {
                _onFirstPageCallback((int)context.PageNumber);
                _firstPageReported = true;
            }

            // For margins, use the QuestPDF page number (not offset) to determine odd/even
            // This ensures first page is always right-hand (odd)
            var isOdd = context.PageNumber % 2 == 1;

            // For displayed page number, apply the offset
            var displayPageNumber = (int)context.PageNumber + _pageNumberOffset;

            float availHeight = Convert.ToSingle(context.AvailableSize.Height);
            float availWidth = Convert.ToSingle(context.AvailableSize.Width);

            // For bottom page numbers: reduce available height so content stops before footer
            // Footer is ~2 lines tall (0.3 inches = ~22 points)
            bool needsBottomFooter = _showPageNumbers &&
                (_theme.PageNumberPosition == Models.PageNumberPosition.Bottom ||
                 _theme.PageNumberPosition == Models.PageNumberPosition.BottomCentered);

            bool willHaveTitle = !state.TitleRendered && !string.IsNullOrWhiteSpace(_chapterTitle);

            if (needsBottomFooter && !willHaveTitle)
            {
                availHeight -= 22f; // ~0.3 inches in points
            }

            // For book binding:
            // Odd pages (1,3,5... right side of spread): spine on LEFT, outer edge on RIGHT
            // Even pages (2,4,6... left side of spread): outer edge on LEFT, spine on RIGHT
            // InsideMargin = spine (binding) margin, OutsideMargin = outer edge margin
            float leftMargin = isOdd ? (float)_theme.InsideMargin : (float)_theme.OutsideMargin;
            float rightMargin = isOdd ? (float)_theme.OutsideMargin : (float)_theme.InsideMargin;

            // Helper: build a column element from fragments and measure its total height (constrained width).
            float MeasureFragmentsHeight(IEnumerable<(string Type, string Text, bool Indent, int ParagraphIndex, int RunOffset)> fragments)
            {
                var temp = context.CreateElement(e =>
                {
                    e.Width(availWidth, Unit.Point)
                     .PaddingLeft(leftMargin, Unit.Inch)
                     .PaddingRight(rightMargin, Unit.Inch)
                     .Column(col =>
                     {
                         // Check if this page will have a chapter title
                         bool hasTitle = fragments.Any(f => f.Type == "Title");

                         // Add top offset if this is the first page of the chapter
                         // Use State property to match what render will see
                         bool isFirstPage = hasTitle && State.ParagraphIndex == 0 && State.CharacterOffset == 0 && !State.TitleRendered;
                         if (isFirstPage)
                         {
                             col.Item().Height((float)_theme.ChapterHeadingTopOffset, Unit.Inch);
                         }

                         // Add space for page number header/footer if enabled (and no title)
                         if (_showPageNumbers && !hasTitle)
                         {
                             if (_theme.PageNumberPosition == Models.PageNumberPosition.Top)
                             {
                                 col.Item().Height(0.5f, Unit.Inch);
                             }
                         }

                         bool isFirst = true;
                         foreach (var f in fragments)
                         {
                             switch (f.Type)
                             {
                                 case "Title":
                                     isFirst = false;

                                     col.Item().Text(t =>
                                     {
                                         var s = t.Span(f.Text);
                                         s.FontFamily(_theme.ChapterTitleFont);
                                         s.FontSize(_theme.ChapterTitleFontSize);
                                         if (_theme.ChapterTitleBold) s.SemiBold();
                                         if (_theme.ChapterTitleItalic) s.Italic();
                                         ApplyAlignment(t, _theme.ChapterTitleAlignment);
                                     });
                                     // Add spacing after title
                                     col.Item().Height((float)_theme.ChapterTitleBottomSpacing, Unit.Inch);
                                     break;
                                 case "Subtitle":
                                     isFirst = false;
                                     col.Item().Text(t =>
                                     {
                                         var s = t.Span(f.Text);
                                         s.FontFamily(_theme.ChapterSubtitleFont);
                                         s.FontSize(_theme.ChapterSubtitleFontSize);
                                         if (_theme.ChapterSubtitleBold) s.SemiBold();
                                         if (_theme.ChapterSubtitleItalic) s.Italic();
                                         ApplyAlignment(t, _theme.ChapterSubtitleAlignment);
                                     });
                                     // Add spacing after subtitle
                                     col.Item().Height((float)_theme.ChapterSubtitleBottomSpacing, Unit.Inch);
                                     break;
                                 case "Para":
                                 case "Chunk":
                                     isFirst = false;
                                     col.Item().PaddingBottom(8).Text(t =>
                                     {
                                         // Strip Markdown formatting for accurate measurement
                                         string textForMeasurement = StripMarkdownFormatting(f.Text);

                                         if (f.Indent)
                                         {
                                             int numSpaces = (int)Math.Ceiling(_theme.ParagraphIndent * 8);
                                             string indentSpaces = new string('\u00A0', numSpaces);
                                             t.Span(indentSpaces + textForMeasurement).FontFamily(_theme.BodyFont).FontSize(_theme.BodyFontSize).LineHeight((float)_theme.LineSpacing);
                                         }
                                         else
                                         {
                                             t.Span(textForMeasurement).FontFamily(_theme.BodyFont).FontSize(_theme.BodyFontSize).LineHeight((float)_theme.LineSpacing);
                                         }
                                         t.Justify();
                                     });
                                     break;
                             }
                         }
                     });
                });

                if (temp is IDynamicElement d)
                    return Convert.ToSingle(d.Size.Height);

                return float.MaxValue;
            }

            // Fragments accepted for the page.
            var fragments = new List<(string Type, string Text, bool Indent, int ParagraphIndex, int RunOffset)>();
            // Track measured total height of accepted fragments to compute incremental heights.
            float currentMeasuredTotal = 0f;
            float remaining = availHeight;

            // Title
            if (!state.TitleRendered && !string.IsNullOrWhiteSpace(_chapterTitle))
            {
                var candidate = ("Title", _chapterTitle, false, -1, 0);
                float after = MeasureFragmentsHeight(fragments.Concat(new[] { candidate }));
                float inc = after - currentMeasuredTotal;
                if (inc <= remaining)
                {
                    fragments.Add(candidate);
                    currentMeasuredTotal = after;
                    remaining -= inc;
                    state.TitleRendered = true;
                }
                else if (after <= availHeight)
                {
                    // can render title alone on fresh page
                    fragments.Add(candidate);
                    currentMeasuredTotal = after;
                    remaining = availHeight - after;
                    state.TitleRendered = true;
                }
                else
                {
                    fragments.Add(candidate);
                    currentMeasuredTotal = after;
                    remaining = -1f;
                    state.TitleRendered = true;
                }
            }

            // Subtitle
            if (state.TitleRendered && !state.SubtitleRendered && !string.IsNullOrWhiteSpace(_chapterSubtitle))
            {
                var candidate = ("Subtitle", _chapterSubtitle, false, -1, 0);
                float after = MeasureFragmentsHeight(fragments.Concat(new[] { candidate }));
                float inc = after - currentMeasuredTotal;
                if (inc <= remaining)
                {
                    fragments.Add(candidate);
                    currentMeasuredTotal = after;
                    remaining -= inc;
                    state.SubtitleRendered = true;
                }
                else if (after <= availHeight && remaining == availHeight)
                {
                    fragments.Add(candidate);
                    currentMeasuredTotal = after;
                    remaining = availHeight - after;
                    state.SubtitleRendered = true;
                }
                // else leave for next page
            }

            // Paragraphs packing
            while (state.ParagraphIndex < _paragraphs.Count)
            {
                var fullParagraph = _paragraphs[state.ParagraphIndex];

                // Get the remaining text in this paragraph starting from our character offset
                var full = fullParagraph.Substring(state.CharacterOffset);

                if (string.IsNullOrWhiteSpace(full))
                {
                    state.ParagraphIndex++;
                    state.CharacterOffset = 0;
                    continue;
                }

                // First paragraph (index 0) should NOT be indented
                // All subsequent paragraphs (index > 0) SHOULD be indented
                // BUT: if we're continuing a paragraph from previous page (CharacterOffset > 0), don't indent
                bool shouldIndent = state.ParagraphIndex > 0 && state.CharacterOffset == 0;

                // Try whole paragraph
                var candidatePara = ("Para", full, shouldIndent, state.ParagraphIndex, state.CharacterOffset);
                float afterPara = MeasureFragmentsHeight(fragments.Concat(new[] { candidatePara }));
                float incPara = afterPara - currentMeasuredTotal;
                if (incPara <= remaining)
                {
                    fragments.Add(candidatePara);
                    currentMeasuredTotal = afterPara;
                    remaining -= incPara;
                    state.ParagraphIndex++;
                    state.CharacterOffset = 0;
                    continue;
                }

                // Paragraph doesn't fit whole. Try to split it.
                // Binary search for largest chunk that fits in remaining space
                int low = 1, high = full.Length;
                string bestChunkText = string.Empty;
                int bestOriginalLength = 0; // Track length in original text before TrimEnd
                while (low <= high)
                {
                    int mid = (low + high) / 2;
                    string candidateText = TakeChunkAtWordBoundary(full, mid);
                    if (string.IsNullOrEmpty(candidateText))
                        candidateText = full.Substring(0, Math.Min(mid, full.Length));

                    var candidateChunk = ("Chunk", candidateText, shouldIndent, state.ParagraphIndex, state.CharacterOffset);
                    float afterChunk = MeasureFragmentsHeight(fragments.Concat(new[] { candidateChunk }));
                    float incChunk = afterChunk - currentMeasuredTotal;

                    if (incChunk <= remaining)
                    {
                        bestChunkText = candidateText;
                        // Find the original length before trimming
                        // TakeChunkAtWordBoundary cuts at word boundary and trims
                        // We need to know where we are in the original `full` text
                        int cutPos = full.LastIndexOf(' ', Math.Min(mid, full.Length - 1));
                        if (cutPos <= 0) cutPos = Math.Min(mid, full.Length);
                        bestOriginalLength = cutPos;
                        low = mid + 1;
                    }
                    else
                    {
                        high = mid - 1;
                    }
                }

                if (!string.IsNullOrEmpty(bestChunkText))
                {
                    // Found a chunk that fits
                    var chunkCandidate = ("Chunk", bestChunkText, shouldIndent, state.ParagraphIndex, state.CharacterOffset);
                    float afterChunk = MeasureFragmentsHeight(fragments.Concat(new[] { chunkCandidate }));
                    float incChunk = afterChunk - currentMeasuredTotal;

                    fragments.Add(chunkCandidate);
                    currentMeasuredTotal = afterChunk;

                    // Update character offset based on original length (before trimming)
                    state.CharacterOffset += bestOriginalLength;

                    // Trim leading whitespace from next chunk
                    while (state.CharacterOffset < fullParagraph.Length &&
                           char.IsWhiteSpace(fullParagraph[state.CharacterOffset]))
                    {
                        state.CharacterOffset++;
                    }

                    remaining -= incChunk;

                    // Check if we've consumed the entire paragraph
                    if (state.CharacterOffset >= fullParagraph.Length)
                    {
                        state.ParagraphIndex++;
                        state.CharacterOffset = 0;
                    }

                    break; // page full, continue on next page
                }
                else
                {
                    // Nothing fits in remaining space
                    // If we have content already on this page, stop here
                    if (fragments.Count > 0 && remaining < availHeight)
                    {
                        break;
                    }

                    // Otherwise try to fit at least one word on a fresh page
                    var words = full.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    string firstWord = words.FirstOrDefault() ?? full.Substring(0, Math.Min(1, full.Length));
                    var firstCandidate = ("Chunk", firstWord, shouldIndent, state.ParagraphIndex, state.CharacterOffset);
                    float afterWord = MeasureFragmentsHeight(fragments.Concat(new[] { firstCandidate }));
                    float incWord = afterWord - currentMeasuredTotal;

                    if (incWord <= remaining || incWord <= availHeight)
                    {
                        fragments.Add(firstCandidate);
                        currentMeasuredTotal = afterWord;

                        state.CharacterOffset += firstWord.Length;

                        // Trim leading whitespace
                        while (state.CharacterOffset < fullParagraph.Length &&
                               char.IsWhiteSpace(fullParagraph[state.CharacterOffset]))
                        {
                            state.CharacterOffset++;
                        }

                        remaining -= incWord;

                        if (state.CharacterOffset >= fullParagraph.Length)
                        {
                            state.ParagraphIndex++;
                            state.CharacterOffset = 0;
                        }
                        break;
                    }
                    else
                    {
                        // Even one word doesn't fit - this is rare, just stop
                        break;
                    }
                }
            }

            // Footer if finished - REMOVED, no longer needed
            // if (state.ParagraphIndex >= _paragraphs.Count)
            // {
            //     ...footer code removed...
            // }

            // Final render (single CreateElement)
            var content = context.CreateElement(element =>
            {
                // Check if we need bottom page numbers (to use layers for absolute positioning)
                // Always use layers when bottom position is selected (unless it's a title page)
                bool hasTitle = fragments.Any(f => f.Type == "Title");
                bool needsBottomPageNumber = _showPageNumbers &&
                    !hasTitle &&
                    (_theme.PageNumberPosition == Models.PageNumberPosition.Bottom ||
                     _theme.PageNumberPosition == Models.PageNumberPosition.BottomCentered);

                IContainer container = element;

                if (needsBottomPageNumber)
                {
                    // Use layers: primary layer MUST extend to full page height
                    // to ensure footer layer's AlignBottom works correctly
                    container.Layers(layers =>
                    {
                        // Main content layer - extend to full height with MinHeight
                        layers.PrimaryLayer()
                            .MinHeight(availHeight, Unit.Point)
                            .PaddingLeft(leftMargin, Unit.Inch)
                            .PaddingRight(rightMargin, Unit.Inch)
                            .Column(col =>
                            {
                                RenderMainContent(col, fragments, isOdd, displayPageNumber);
                            });

                        // Footer layer - positioned at absolute bottom (where we reserved 22 points)
                        layers.Layer()
                            .AlignBottom()
                            .PaddingLeft(leftMargin, Unit.Inch)
                            .PaddingRight(rightMargin, Unit.Inch)
                            .Height(22f, Unit.Point)
                            .Column(col =>
                            {
                                if (_theme.PageNumberPosition == Models.PageNumberPosition.Bottom)
                                {
                                    RenderPageNumberBottom(col.Item(), isOdd, displayPageNumber);
                                }
                                else if (_theme.PageNumberPosition == Models.PageNumberPosition.BottomCentered)
                                {
                                    RenderPageNumberBottomCentered(col.Item(), displayPageNumber);
                                }
                            });
                    });
                }
                else
                {
                    // No bottom footer, use simple column
                    IContainer margin = container;
                    margin = margin.PaddingLeft(leftMargin, Unit.Inch).PaddingRight(rightMargin, Unit.Inch);

                    margin.Column(col =>
                    {
                        RenderMainContent(col, fragments, isOdd, displayPageNumber);
                    });
                }
            });

            State = state;
            bool hasMore = state.ParagraphIndex < _paragraphs.Count;

            return new DynamicComponentComposeResult
            {
                Content = content,
                HasMoreContent = hasMore
            };
        }

        private void RenderMainContent(ColumnDescriptor col, List<(string Type, string Text, bool Indent, int ParagraphIndex, int RunOffset)> fragments, bool isOdd, int displayPageNumber)
        {
                // Check if this page will have a chapter title (don't show page number if so)
                bool hasTitle = fragments.Any(f => f.Type == "Title");

                // Add top offset if this is the first page of the chapter
                // Check the ORIGINAL State property, not the modified local state variable
                bool isFirstPage = hasTitle && State.ParagraphIndex == 0 && State.CharacterOffset == 0 && !State.TitleRendered;
                if (isFirstPage)
                {
                    col.Item().Height((float)_theme.ChapterHeadingTopOffset, Unit.Inch);
                }

                // Add page number header at top if enabled and no title on this page
                if (_showPageNumbers && !hasTitle && _theme.PageNumberPosition == Models.PageNumberPosition.Top)
                {
                    RenderPageNumberTop(col.Item(), isOdd, displayPageNumber);
                }

                bool isFirstFragment = true;
                foreach (var f in fragments)
                {
                    switch (f.Type)
                    {
                        case "Title":
                            isFirstFragment = false;

                            col.Item().Text(t =>
                            {
                                var s = t.Span(f.Text);
                                s.FontFamily(_theme.ChapterTitleFont);
                                s.FontSize(_theme.ChapterTitleFontSize);
                                if (_theme.ChapterTitleBold) s.SemiBold();
                                if (_theme.ChapterTitleItalic) s.Italic();
                                ApplyAlignment(t, _theme.ChapterTitleAlignment);
                            });
                            // Add spacing after title
                            col.Item().Height((float)_theme.ChapterTitleBottomSpacing, Unit.Inch);
                            break;
                        case "Subtitle":
                            isFirstFragment = false;
                            col.Item().Text(t =>
                            {
                                var s = t.Span(f.Text);
                                s.FontFamily(_theme.ChapterSubtitleFont);
                                s.FontSize(_theme.ChapterSubtitleFontSize);
                                if (_theme.ChapterSubtitleBold) s.SemiBold();
                                if (_theme.ChapterSubtitleItalic) s.Italic();
                                ApplyAlignment(t, _theme.ChapterSubtitleAlignment);
                            });
                            // Add spacing after subtitle
                            col.Item().Height((float)_theme.ChapterSubtitleBottomSpacing, Unit.Inch);
                            break;
                        case "Para":
                        case "Chunk":
                            isFirstFragment = false;
                            col.Item().PaddingBottom(8).Text(t =>
                            {
                                // Apply first-line indent using non-breaking spaces
                                if (f.Indent)
                                {
                                    // Use non-breaking spaces which won't be trimmed
                                    int numSpaces = (int)Math.Ceiling(_theme.ParagraphIndent * 8);
                                    string indentSpaces = new string('\u00A0', numSpaces);
                                    t.Span(indentSpaces);
                                }

                                // Render the Markdown text that was stored in f.Text
                                // This is the SAME text we measured, ensuring alignment
                                RenderMarkdownInlines(t, f.Text);

                                t.Justify();
                            });
                            break;
                    }
                }
        }

        private void RenderMarkdownInlines(TextDescriptor textDescriptor, string markdown)
        {
            // Parse the markdown with hard line breaks enabled
            var pipeline = new MarkdownPipelineBuilder()
                .UseEmphasisExtras() // Enables ~~strikethrough~~ and other extras
                .UseSoftlineBreakAsHardlineBreak() // Treat single newlines as line breaks
                .Build();
            var document = Markdown.Parse(markdown, pipeline);

            // Process inline elements
            foreach (var block in document)
            {
                if (block is ParagraphBlock paragraphBlock)
                {
                    RenderInlineContainer(textDescriptor, paragraphBlock.Inline);
                }
            }
        }

        private void RenderInlineContainer(TextDescriptor textDescriptor, ContainerInline? container)
        {
            if (container == null) return;

            foreach (var inline in container)
            {
                RenderInline(textDescriptor, inline);
            }
        }

        private void RenderInline(TextDescriptor textDescriptor, Inline inline)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    textDescriptor.Span(literal.Content.ToString())
                        .FontFamily(_theme.BodyFont)
                        .FontSize(_theme.BodyFontSize)
                        .LineHeight((float)_theme.LineSpacing);
                    break;

                case EmphasisInline emphasis:
                    // Recursively render all children with formatting applied
                    RenderEmphasisChildren(textDescriptor, emphasis);
                    break;

                case HtmlInline html:
                    // Skip HTML tags - they're just markup
                    break;

                case LineBreakInline lineBreak:
                    // Render as a line break, not just a space
                    // Check if it's a hard break (explicit) or soft break
                    if (lineBreak.IsHard)
                    {
                        textDescriptor.Span("\n"); // Hard line break
                    }
                    else
                    {
                        textDescriptor.Span(" "); // Soft break becomes space
                    }
                    break;

                case ContainerInline containerInline:
                    RenderInlineContainer(textDescriptor, containerInline);
                    break;

                default:
                    // For unknown inline types, try to render any children if it's a container
                    if (inline is ContainerInline unknownContainer)
                    {
                        RenderInlineContainer(textDescriptor, unknownContainer);
                    }
                    break;
            }
        }

        private void RenderEmphasisChildren(TextDescriptor textDescriptor, EmphasisInline emphasis)
        {
            foreach (var child in emphasis)
            {
                if (child is LiteralInline emphLiteral)
                {
                    var span = textDescriptor.Span(emphLiteral.Content.ToString());
                    span.FontFamily(_theme.BodyFont);
                    span.FontSize(_theme.BodyFontSize);
                    span.LineHeight((float)_theme.LineSpacing);

                    // Check the delimiter character to determine formatting
                    // + = inserted (underline), * = italic, ** = bold
                    if (emphasis.DelimiterChar == '+') // ++underline++
                        span.Underline();
                    else if (emphasis.DelimiterCount == 2) // **bold** or __bold__
                        span.Bold();
                    else if (emphasis.DelimiterCount == 1) // *italic* or _italic_
                        span.Italic();
                }
                else if (child is EmphasisInline nestedEmphasis)
                {
                    // Handle nested emphasis (e.g., ***bold italic***)
                    RenderEmphasisChildren(textDescriptor, nestedEmphasis);
                }
                else
                {
                    // Recursively handle other inline types
                    RenderInline(textDescriptor, child);
                }
            }
        }

        private string TakeChunkAtWordBoundary(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (text.Length <= maxChars) return text;

            int cut = text.LastIndexOf(' ', Math.Min(maxChars, text.Length - 1));
            if (cut <= 0) cut = Math.Min(maxChars, text.Length);
            return text.Substring(0, cut).TrimEnd();
        }

        private void ApplyAlignment(TextDescriptor text, Models.TextAlignment alignment)
        {
            switch (alignment)
            {
                case Models.TextAlignment.Left:
                    text.AlignLeft();
                    break;
                case Models.TextAlignment.Center:
                    text.AlignCenter();
                    break;
                case Models.TextAlignment.Right:
                    text.AlignRight();
                    break;
                case Models.TextAlignment.Justify:
                    text.Justify();
                    break;
            }
        }

        private string GetHeaderText(Models.HeaderContentType contentType, bool capitalize)
        {
            string text = contentType switch
            {
                Models.HeaderContentType.Title => _bookTitle,
                Models.HeaderContentType.Author => _bookAuthor,
                Models.HeaderContentType.ChapterTitle => _chapterTitle,
                _ => ""
            };

            return capitalize ? text.ToUpper() : text;
        }

        private void RenderPageNumberTop(IContainer container, bool isOdd, int displayPageNumber)
        {
            container.Height(0.5f, Unit.Inch).AlignMiddle().Row(row =>
            {
                // Page number goes on the outside edge
                // Header content goes in the center
                // Odd pages (right side): outside is on RIGHT
                // Even pages (left side): outside is on LEFT
                if (isOdd)
                {
                    // Right-hand page: number on right (outside edge)
                    row.AutoItem().AlignLeft().Text("");
                    row.RelativeItem().AlignCenter().Text(t =>
                    {
                        var headerText = GetHeaderText(_theme.RightPageHeaderContent, _theme.RightPageHeaderCapitalize);
                        if (!string.IsNullOrWhiteSpace(headerText))
                        {
                            t.DefaultTextStyle(x => x.FontFamily(_theme.BodyFont).FontSize(_theme.BodyFontSize));
                            t.Span(headerText);
                        }
                    });
                    row.AutoItem().AlignRight().Text(t =>
                    {
                        var span = t.Span(displayPageNumber.ToString());
                        span.FontFamily(_theme.BodyFont);
                        span.FontSize(_theme.BodyFontSize);
                    });
                }
                else
                {
                    // Left-hand page: number on left (outside edge)
                    row.AutoItem().AlignLeft().Text(t =>
                    {
                        var span = t.Span(displayPageNumber.ToString());
                        span.FontFamily(_theme.BodyFont);
                        span.FontSize(_theme.BodyFontSize);
                    });
                    row.RelativeItem().AlignCenter().Text(t =>
                    {
                        var headerText = GetHeaderText(_theme.LeftPageHeaderContent, _theme.LeftPageHeaderCapitalize);
                        if (!string.IsNullOrWhiteSpace(headerText))
                        {
                            t.DefaultTextStyle(x => x.FontFamily(_theme.BodyFont).FontSize(_theme.BodyFontSize));
                            t.Span(headerText);
                        }
                    });
                    row.AutoItem().AlignRight().Text("");
                }
            });
        }

        private void RenderPageNumberBottom(IContainer container, bool isOdd, int displayPageNumber)
        {
            container.AlignMiddle().Row(row =>
            {
                // Same layout as top - page number on outside edge
                if (isOdd)
                {
                    // Right-hand page: number on right (outside edge)
                    row.AutoItem().AlignLeft().Text("");
                    row.RelativeItem().AlignCenter().Text("");
                    row.AutoItem().AlignRight().Text(t =>
                    {
                        var span = t.Span(displayPageNumber.ToString());
                        span.FontFamily(_theme.BodyFont);
                        span.FontSize(_theme.BodyFontSize);
                    });
                }
                else
                {
                    // Left-hand page: number on left (outside edge)
                    row.AutoItem().AlignLeft().Text(t =>
                    {
                        var span = t.Span(displayPageNumber.ToString());
                        span.FontFamily(_theme.BodyFont);
                        span.FontSize(_theme.BodyFontSize);
                    });
                    row.RelativeItem().AlignCenter().Text("");
                    row.AutoItem().AlignRight().Text("");
                }
            });
        }

        private void RenderPageNumberBottomCentered(IContainer container, int displayPageNumber)
        {
            container.AlignCenter().AlignMiddle().Text(t =>
            {
                var span = t.Span(displayPageNumber.ToString());
                span.FontFamily(_theme.BodyFont);
                span.FontSize(_theme.BodyFontSize);
            });
        }
    }
}