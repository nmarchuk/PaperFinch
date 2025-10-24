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
        private readonly bool _isTableOfContents;
        private readonly bool _noIndent;
        private readonly bool _excludeFromPageCount;

        public AlternatingMarginContent(string chapterTitle, string chapterSubtitle, string markdownContent, Models.PdfTheme theme, int pageNumberOffset = 0, bool showPageNumbers = false, string bookTitle = "", string bookAuthor = "", Action<int>? onFirstPage = null, bool isTableOfContents = false, bool noIndent = false, bool excludeFromPageCount = false)
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
            _isTableOfContents = isTableOfContents;
            _noIndent = noIndent;
            _excludeFromPageCount = excludeFromPageCount;

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

                                     // Check if this is the first paragraph's first fragment (for drop caps)
                                     bool isFirstParagraphStart = (f.ParagraphIndex == 0 && f.RunOffset == 0);
                                     bool shouldUseDropCap = _theme.DropCaps && isFirstParagraphStart && !_isTableOfContents && !_noIndent && !_excludeFromPageCount && !string.IsNullOrEmpty(f.Text);

                                     // Calculate paragraph spacing: normal (8pt) or double-spaced (blank line based on font size * line spacing)
                                     float paragraphSpacing = _theme.DoubleSpaceParagraphs
                                         ? (float)(_theme.BodyFontSize * _theme.LineSpacing)
                                         : 8f;

                                     if (shouldUseDropCap)
                                     {
                                         // Measure drop cap layout with text wrapping
                                         col.Item().PaddingBottom(paragraphSpacing).Column(dropCapCol =>
                                         {
                                             dropCapCol.Spacing(0); // Remove spacing between items so text flows naturally

                                             var (firstCharMarkdown, remainingMarkdown) = SplitFirstCharacter(f.Text);
                                             float dropCapSize = (float)(_theme.BodyFontSize * _theme.LineSpacing * 2);
                                             float dropCapWidth = dropCapSize * 0.7f;

                                             // Calculate actual available width: full width minus margins minus drop cap width minus padding
                                             float actualTextWidth = availWidth - ((leftMargin + rightMargin) * 72f); // Convert inches to points
                                             float textWidthBesideDropCap = actualTextWidth - dropCapWidth - 3;

                                             var (textBesideDropCap, textBelowDropCap) = SplitTextForDropCap(
                                                 remainingMarkdown,
                                                 textWidthBesideDropCap,
                                                 dropCapSize,
                                                 _theme.BodyFont,
                                                 _theme.BodyFontSize,
                                                 (float)_theme.LineSpacing,
                                                 _theme.LeadWithSmallCaps);

                                             // First row: Drop cap + text beside it
                                             dropCapCol.Item().Row(row =>
                                             {
                                                 // Left: drop cap
                                                 row.ConstantItem(dropCapWidth).AlignTop().PaddingTop(-dropCapSize * 0.25f).Text(t =>
                                                 {
                                                     RenderMarkdownInlines(t, firstCharMarkdown, _theme.DropCapFont, dropCapSize);
                                                 });

                                                 // Right: first portion of text
                                                 row.RelativeItem().AlignTop().PaddingLeft(3).Text(t =>
                                                 {
                                                     if (!string.IsNullOrEmpty(textBesideDropCap))
                                                     {
                                                         // Apply small caps if enabled (drop cap already handles first character)
                                                         if (_theme.LeadWithSmallCaps)
                                                             RenderMarkdownWithSmallCaps(t, textBesideDropCap);
                                                         else
                                                             RenderMarkdownInlines(t, textBesideDropCap);
                                                         t.Justify();
                                                     }
                                                 });
                                             });

                                             // Second row: Remaining text at full width
                                             if (!string.IsNullOrEmpty(textBelowDropCap))
                                             {
                                                 // Use PaddingTop to adjust spacing - pull text up to remove gap
                                                 // Line spacing creates some space, so we compensate
                                                 float lineSpacingOffset = (float)(_theme.BodyFontSize * (_theme.LineSpacing - 1) * 1.15f);
                                                 dropCapCol.Item().PaddingTop(-lineSpacingOffset).Text(t =>
                                                 {
                                                     RenderMarkdownInlines(t, textBelowDropCap);
                                                     t.Justify();
                                                 });
                                             }
                                         });
                                     }
                                     else
                                     {
                                         col.Item().PaddingBottom(paragraphSpacing).Text(t =>
                                         {
                                             if (f.Indent)
                                             {
                                                 int numSpaces = (int)Math.Ceiling(_theme.ParagraphIndent * 8);
                                                 string indentSpaces = new string('\u00A0', numSpaces);
                                                 t.Span(indentSpaces);
                                             }

                                             // Use the same rendering approach as the actual render to ensure measurements match
                                             bool shouldUseSmallCaps = _theme.LeadWithSmallCaps && (f.ParagraphIndex == 0 && f.RunOffset == 0) && !_isTableOfContents && !_noIndent && !_excludeFromPageCount;
                                             if (shouldUseSmallCaps)
                                                 RenderMarkdownWithSmallCaps(t, f.Text);
                                             else
                                                 RenderMarkdownInlines(t, f.Text);

                                             t.Justify();
                                         });
                                     }
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
                if (state.CharacterOffset >= fullParagraph.Length)
                {
                    // We've consumed the entire paragraph
                    state.ParagraphIndex++;
                    state.CharacterOffset = 0;
                    continue;
                }

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
                // ALSO: Table of Contents chapters and NoIndent chapters should never have paragraph indents
                bool shouldIndent = !_isTableOfContents && !_noIndent && state.ParagraphIndex > 0 && state.CharacterOffset == 0;

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

                    // Calculate how much of the original text to skip
                    // The bestChunkText is trimmed, but we need to advance past it in the full paragraph
                    int chunkLengthInFull = bestChunkText.Length;

                    // Skip any trailing whitespace after the chunk text in the original full text
                    while (chunkLengthInFull < full.Length && char.IsWhiteSpace(full[chunkLengthInFull]))
                    {
                        chunkLengthInFull++;
                    }

                    state.CharacterOffset += chunkLengthInFull;

                    // Trim leading whitespace from next chunk (redundant but safe)
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
                                RenderMainContent(col, fragments, isOdd, displayPageNumber, availWidth, leftMargin, rightMargin);
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
                        RenderMainContent(col, fragments, isOdd, displayPageNumber, availWidth, leftMargin, rightMargin);
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

        private void RenderMainContent(ColumnDescriptor col, List<(string Type, string Text, bool Indent, int ParagraphIndex, int RunOffset)> fragments, bool isOdd, int displayPageNumber, float availWidth, float leftMargin, float rightMargin)
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

                            // Special rendering for TOC entries with page numbers
                            if (_isTableOfContents && f.Text.Contains("|||"))
                            {
                                col.Item().PaddingBottom(8).Row(row =>
                                {
                                    var parts = f.Text.Split(new[] { "|||" }, StringSplitOptions.None);
                                    if (parts.Length == 2)
                                    {
                                        // Left column: chapter title with dots
                                        row.RelativeItem().Text(t =>
                                        {
                                            t.Span(parts[0].Trim())
                                                .FontFamily(_theme.BodyFont)
                                                .FontSize(_theme.BodyFontSize);
                                            t.Span(" ")
                                                .FontFamily(_theme.BodyFont)
                                                .FontSize(_theme.BodyFontSize);
                                        });

                                        // Right column: page number (right-aligned)
                                        row.ConstantItem(30).Text(t =>
                                        {
                                            t.Span(parts[1].Trim())
                                                .FontFamily(_theme.BodyFont)
                                                .FontSize(_theme.BodyFontSize);
                                            t.AlignRight();
                                        });
                                    }
                                });
                            }
                            else
                            {
                                // Check if this is the first paragraph's first fragment (for drop caps)
                                bool isFirstParagraphStart = (f.ParagraphIndex == 0 && f.RunOffset == 0);
                                bool shouldUseDropCap = _theme.DropCaps && isFirstParagraphStart && !_isTableOfContents && !_noIndent && !_excludeFromPageCount && !string.IsNullOrEmpty(f.Text);

                                // Calculate paragraph spacing: normal (8pt) or double-spaced (blank line based on font size * line spacing)
                                float paragraphSpacing = _theme.DoubleSpaceParagraphs
                                    ? (float)(_theme.BodyFontSize * _theme.LineSpacing)
                                    : 8f;

                                if (shouldUseDropCap)
                                {
                                    // Drop cap rendering with proper text wrapping
                                    col.Item().PaddingBottom(paragraphSpacing).Column(dropCapCol =>
                                    {
                                        dropCapCol.Spacing(0); // Remove spacing between items so text flows naturally

                                        // Extract first character and remaining text
                                        var (firstCharMarkdown, remainingMarkdown) = SplitFirstCharacter(f.Text);

                                        // Calculate drop cap size (2 lines tall)
                                        float dropCapSize = (float)(_theme.BodyFontSize * _theme.LineSpacing * 2);
                                        float dropCapWidth = dropCapSize * 0.7f;

                                        // Calculate available width for text beside drop cap
                                        // Use actual available width based on page width and margins
                                        float actualTextWidth = availWidth - ((leftMargin + rightMargin) * 72f); // Convert inches to points
                                        float textWidthBesideDropCap = actualTextWidth - dropCapWidth - 3;

                                        // Split the remaining text into two parts:
                                        // 1. Text that fits beside the drop cap (roughly 2 lines)
                                        // 2. Text that continues below
                                        var (textBesideDropCap, textBelowDropCap) = SplitTextForDropCap(
                                            remainingMarkdown,
                                            textWidthBesideDropCap,
                                            dropCapSize,
                                            _theme.BodyFont,
                                            _theme.BodyFontSize,
                                            (float)_theme.LineSpacing,
                                            _theme.LeadWithSmallCaps);

                                        // First row: Drop cap + text beside it
                                        dropCapCol.Item().Row(row =>
                                        {
                                            // Left: Drop cap
                                            row.ConstantItem(dropCapWidth).AlignTop().PaddingTop(-dropCapSize * 0.25f).Text(t =>
                                            {
                                                RenderMarkdownInlines(t, firstCharMarkdown, _theme.DropCapFont, dropCapSize);
                                            });

                                            // Right: First portion of text
                                            row.RelativeItem().AlignTop().PaddingLeft(3).Text(t =>
                                            {
                                                if (!string.IsNullOrEmpty(textBesideDropCap))
                                                {
                                                    // Apply small caps if enabled (drop cap already handles first character)
                                                    if (_theme.LeadWithSmallCaps)
                                                        RenderMarkdownWithSmallCaps(t, textBesideDropCap);
                                                    else
                                                        RenderMarkdownInlines(t, textBesideDropCap);
                                                    t.Justify();
                                                }
                                            });
                                        });

                                        // Second row: Remaining text at full width (if any)
                                        if (!string.IsNullOrEmpty(textBelowDropCap))
                                        {
                                            // Use PaddingTop to adjust spacing - pull text up to remove gap
                                            // Line spacing creates some space, so we compensate
                                            float lineSpacingOffset = (float)(_theme.BodyFontSize * (_theme.LineSpacing - 1) * 1.15f);
                                            dropCapCol.Item().PaddingTop(-lineSpacingOffset).Text(t =>
                                            {
                                                RenderMarkdownInlines(t, textBelowDropCap);
                                                t.Justify();
                                            });
                                        }
                                    });
                                }
                                else
                                {
                                    col.Item().PaddingBottom(paragraphSpacing).Text(t =>
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
                                        // Apply small caps for first paragraph if enabled
                                        bool shouldUseSmallCaps = _theme.LeadWithSmallCaps && isFirstParagraphStart && !_isTableOfContents && !_noIndent && !_excludeFromPageCount;
                                        if (shouldUseSmallCaps)
                                            RenderMarkdownWithSmallCaps(t, f.Text);
                                        else
                                            RenderMarkdownInlines(t, f.Text);

                                        // Table of Contents should use left alignment so spaces aren't distributed
                                        if (_isTableOfContents)
                                            t.AlignLeft();
                                        else
                                            t.Justify();
                                    });
                                }
                            }
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

        /// <summary>
        /// Splits markdown text to extract the first character (preserving formatting) and the rest.
        /// Handles cases like "**T**he" or "*I*t was" where first char has formatting.
        /// </summary>
        private (string firstChar, string remaining) SplitFirstCharacter(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return (string.Empty, string.Empty);

            // Simple approach: extract the first visible character
            // Check if starts with formatting markers
            var trimmed = markdown.TrimStart();

            if (trimmed.StartsWith("**") || trimmed.StartsWith("__")) // Bold
            {
                // Find first character after opening markers
                int markerLen = 2;
                if (trimmed.Length > markerLen)
                {
                    string firstChar = trimmed.Substring(0, markerLen + 1); // "**T"

                    // Check if we need to close the formatting around just this char
                    // Look for the closing marker
                    int closePos = trimmed.IndexOf(trimmed.Substring(0, markerLen), markerLen);

                    if (closePos > markerLen) // Found closing marker
                    {
                        // Extract: "**T**"
                        firstChar = trimmed.Substring(0, closePos + markerLen);
                        string remaining = trimmed.Substring(closePos + markerLen);
                        return (firstChar, remaining);
                    }
                    else
                    {
                        // No closing found, just take first char with opening marker
                        firstChar = trimmed.Substring(0, markerLen + 1) + trimmed.Substring(0, markerLen); // "**T**"
                        string remaining = trimmed.Substring(markerLen + 1);
                        return (firstChar, remaining);
                    }
                }
            }
            else if (trimmed.StartsWith("*") || trimmed.StartsWith("_")) // Italic
            {
                int markerLen = 1;
                if (trimmed.Length > markerLen)
                {
                    string firstChar = trimmed.Substring(0, markerLen + 1); // "*T"

                    int closePos = trimmed.IndexOf(trimmed[0], markerLen);

                    if (closePos > markerLen)
                    {
                        firstChar = trimmed.Substring(0, closePos + markerLen);
                        string remaining = trimmed.Substring(closePos + markerLen);
                        return (firstChar, remaining);
                    }
                    else
                    {
                        firstChar = trimmed.Substring(0, markerLen + 1) + trimmed[0]; // "*T*"
                        string remaining = trimmed.Substring(markerLen + 1);
                        return (firstChar, remaining);
                    }
                }
            }
            else if (trimmed.StartsWith("++")) // Underline
            {
                int markerLen = 2;
                if (trimmed.Length > markerLen)
                {
                    string firstChar = trimmed.Substring(0, markerLen + 1) + "++"; // "++T++"
                    string remaining = trimmed.Substring(markerLen + 1);

                    // Look for closing
                    int closePos = trimmed.IndexOf("++", markerLen);
                    if (closePos > markerLen)
                    {
                        firstChar = trimmed.Substring(0, closePos + markerLen);
                        remaining = trimmed.Substring(closePos + markerLen);
                        return (firstChar, remaining);
                    }

                    return (firstChar, remaining);
                }
            }

            // No formatting, just plain text
            string first = trimmed.Substring(0, 1);
            string rest = trimmed.Substring(1);
            return (first, rest);
        }

        /// <summary>
        /// Overload of RenderMarkdownInlines that allows custom font and size (for drop caps)
        /// </summary>
        private void RenderMarkdownInlines(TextDescriptor textDescriptor, string markdown, string customFont, float customSize)
        {
            var pipeline = new MarkdownPipelineBuilder()
                .UseEmphasisExtras()
                .UseSoftlineBreakAsHardlineBreak()
                .Build();
            var document = Markdown.Parse(markdown, pipeline);

            foreach (var block in document)
            {
                if (block is ParagraphBlock paragraphBlock)
                {
                    RenderInlineContainerCustom(textDescriptor, paragraphBlock.Inline, customFont, customSize);
                }
            }
        }

        private void RenderInlineContainerCustom(TextDescriptor textDescriptor, ContainerInline? container, string customFont, float customSize)
        {
            if (container == null) return;

            foreach (var inline in container)
            {
                RenderInlineCustom(textDescriptor, inline, customFont, customSize);
            }
        }

        private void RenderInlineCustom(TextDescriptor textDescriptor, Inline inline, string customFont, float customSize)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    textDescriptor.Span(literal.Content.ToString())
                        .FontFamily(customFont)
                        .FontSize(customSize);
                    break;

                case EmphasisInline emphasis:
                    RenderEmphasisChildrenCustom(textDescriptor, emphasis, customFont, customSize);
                    break;

                case LineBreakInline lineBreak:
                    if (lineBreak.IsHard)
                        textDescriptor.Span("\n");
                    else
                        textDescriptor.Span(" ");
                    break;

                case ContainerInline containerInline:
                    RenderInlineContainerCustom(textDescriptor, containerInline, customFont, customSize);
                    break;

                default:
                    if (inline is ContainerInline unknownContainer)
                    {
                        RenderInlineContainerCustom(textDescriptor, unknownContainer, customFont, customSize);
                    }
                    break;
            }
        }

        private void RenderEmphasisChildrenCustom(TextDescriptor textDescriptor, EmphasisInline emphasis, string customFont, float customSize)
        {
            foreach (var child in emphasis)
            {
                if (child is LiteralInline literal)
                {
                    var span = textDescriptor.Span(literal.Content.ToString())
                        .FontFamily(customFont)
                        .FontSize(customSize);

                    if (emphasis.DelimiterChar == '*' || emphasis.DelimiterChar == '_')
                    {
                        if (emphasis.DelimiterCount == 2)
                            span.SemiBold();
                        else if (emphasis.DelimiterCount == 1)
                            span.Italic();
                    }
                    else if (emphasis.DelimiterChar == '~' && emphasis.DelimiterCount == 2)
                    {
                        span.Strikethrough();
                    }
                    else if (emphasis.DelimiterChar == '+' && emphasis.DelimiterCount == 2)
                    {
                        span.Underline();
                    }
                }
                else if (child is EmphasisInline nestedEmphasis)
                {
                    RenderEmphasisChildrenCustom(textDescriptor, nestedEmphasis, customFont, customSize);
                }
                else
                {
                    RenderInlineCustom(textDescriptor, child, customFont, customSize);
                }
            }
        }

        /// <summary>
        /// Splits text for drop cap rendering - determines how much text fits beside the drop cap
        /// and how much should flow below it using actual QuestPDF measurement.
        /// </summary>
        private (string textBeside, string textBelow) SplitTextForDropCap(
            string markdown,
            float availableWidth,
            float dropCapHeight,
            string font,
            int fontSize,
            float lineSpacing,
            bool useSmallCaps = false)
        {
            if (string.IsNullOrEmpty(markdown))
                return (string.Empty, string.Empty);

            // Strip markdown for measurement purposes
            string plainText = StripMarkdownFormatting(markdown);

            // Split by words to build up text word-by-word
            string[] words = plainText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0)
                return (markdown, string.Empty);

            // Measure text word by word to find how much fits in the available space
            int wordsThatFit = 0;
            int lastSafeWordCount = 0;

            for (int i = 1; i <= words.Length; i++)
            {
                string testText = string.Join(" ", words.Take(i));

                // Measure this text, accounting for small caps if enabled
                Size size;
                if (useSmallCaps)
                {
                    size = MeasureTextWithSmallCaps(testText, font, fontSize, lineSpacing, availableWidth);
                }
                else
                {
                    size = MeasureText(testText, font, fontSize, lineSpacing, availableWidth);
                }

                // Calculate expected height for exactly 2 lines
                float twoLineHeight = fontSize * lineSpacing * 2;

                if (size.Height <= twoLineHeight)
                {
                    // Still within 2 lines
                    lastSafeWordCount = i;
                    wordsThatFit = i;
                }
                else if (size.Height <= dropCapHeight)
                {
                    // Within drop cap height but might be getting close to 3 lines
                    // This is the boundary case - be conservative
                    wordsThatFit = i;
                }
                else
                {
                    // Exceeded drop cap height, definitely too much
                    // Use the last safe count if we have one
                    if (lastSafeWordCount > 0)
                    {
                        wordsThatFit = lastSafeWordCount;
                    }
                    break;
                }
            }

            // If no words fit (very unlikely), put first word anyway
            if (wordsThatFit == 0)
                wordsThatFit = 1;

            // If all words fit, return everything
            if (wordsThatFit >= words.Length)
            {
                return (markdown, string.Empty);
            }

            // Build the split based on word count
            string textForBeside = string.Join(" ", words.Take(wordsThatFit));
            string textForBelow = string.Join(" ", words.Skip(wordsThatFit));

            // Now split the markdown at the corresponding position
            // Find where textForBeside ends in the original markdown
            string markdownLower = markdown.ToLower();
            string textForBesideLower = textForBeside.ToLower();

            int splitPoint = markdownLower.IndexOf(textForBesideLower);
            if (splitPoint >= 0)
            {
                splitPoint += textForBeside.Length;

                // Find next space to avoid cutting markdown formatting
                while (splitPoint < markdown.Length && markdown[splitPoint] != ' ')
                {
                    splitPoint++;
                }
                if (splitPoint < markdown.Length)
                    splitPoint++; // Skip the space
            }
            else
            {
                // Fallback: use character position from plain text
                splitPoint = textForBeside.Length;
            }

            if (splitPoint >= markdown.Length)
            {
                return (markdown, string.Empty);
            }

            string markdownBeside = markdown.Substring(0, splitPoint).TrimEnd();
            string markdownBelow = markdown.Substring(splitPoint).TrimStart();

            return (markdownBeside, markdownBelow);
        }

        /// <summary>
        /// Measures text with small caps applied to determine its rendered size at a given width.
        /// </summary>
        private Size MeasureTextWithSmallCaps(string text, string font, int fontSize, float lineSpacing, float constrainedWidth)
        {
            try
            {
                // Split text for small caps
                var (smallCapsText, remainingText) = SplitForSmallCaps(text);

                using (var paint = new SkiaSharp.SKPaint())
                {
                    paint.Typeface = SkiaSharp.SKTypeface.FromFamilyName(font);

                    // Measure small caps portion (85% size, uppercase)
                    float smallCapsFontSize = fontSize * 0.85f;
                    float totalWidth = 0;
                    float maxHeight = fontSize * lineSpacing;

                    if (!string.IsNullOrEmpty(smallCapsText))
                    {
                        string upperText = StripMarkdownFormatting(smallCapsText).ToUpper();
                        paint.TextSize = smallCapsFontSize;

                        var smallCapsWords = upperText.Split(' ');
                        foreach (var word in smallCapsWords)
                        {
                            totalWidth += paint.MeasureText(word + " ");
                        }
                    }

                    if (!string.IsNullOrEmpty(remainingText))
                    {
                        // Add space if we had small caps
                        if (!string.IsNullOrEmpty(smallCapsText))
                            totalWidth += paint.MeasureText(" ");

                        // Measure remaining text at normal size
                        paint.TextSize = fontSize;
                        string plainRemaining = StripMarkdownFormatting(remainingText);
                        var remainingWords = plainRemaining.Split(' ');
                        foreach (var word in remainingWords)
                        {
                            totalWidth += paint.MeasureText(word + " ");
                        }
                    }

                    // Simulate line wrapping with mixed font sizes
                    float currentLineWidth = 0;
                    float totalHeight = fontSize * lineSpacing;
                    bool isFirstWordOnLine = true;

                    // Process small caps words
                    if (!string.IsNullOrEmpty(smallCapsText))
                    {
                        paint.TextSize = smallCapsFontSize;
                        string upperText = StripMarkdownFormatting(smallCapsText).ToUpper();
                        var smallCapsWords = upperText.Split(' ');

                        foreach (var word in smallCapsWords)
                        {
                            float wordWidth = paint.MeasureText(word);
                            float spaceWidth = paint.MeasureText(" ");
                            float widthToAdd = isFirstWordOnLine ? wordWidth : (spaceWidth + wordWidth);

                            if (currentLineWidth + widthToAdd > constrainedWidth && !isFirstWordOnLine)
                            {
                                currentLineWidth = wordWidth;
                                totalHeight += fontSize * lineSpacing;
                                isFirstWordOnLine = true;
                            }
                            else
                            {
                                currentLineWidth += widthToAdd;
                                isFirstWordOnLine = false;
                            }
                        }
                    }

                    // Process remaining words at normal size
                    if (!string.IsNullOrEmpty(remainingText))
                    {
                        paint.TextSize = fontSize;
                        string plainRemaining = StripMarkdownFormatting(remainingText);
                        var remainingWords = plainRemaining.Split(' ');

                        foreach (var word in remainingWords)
                        {
                            float wordWidth = paint.MeasureText(word);
                            float spaceWidth = paint.MeasureText(" ");
                            float widthToAdd = isFirstWordOnLine ? wordWidth : (spaceWidth + wordWidth);

                            if (currentLineWidth + widthToAdd > constrainedWidth && !isFirstWordOnLine)
                            {
                                currentLineWidth = wordWidth;
                                totalHeight += fontSize * lineSpacing;
                                isFirstWordOnLine = true;
                            }
                            else
                            {
                                currentLineWidth += widthToAdd;
                                isFirstWordOnLine = false;
                            }
                        }
                    }

                    return new Size(currentLineWidth, totalHeight);
                }
            }
            catch
            {
                // Fallback to regular measurement
                return MeasureText(text, font, fontSize, lineSpacing, constrainedWidth);
            }
        }

        /// <summary>
        /// Measures text to determine its rendered size at a given width.
        /// </summary>
        private Size MeasureText(string text, string font, int fontSize, float lineSpacing, float constrainedWidth)
        {
            try
            {
                // Use SkiaSharp to measure text accurately
                using (var paint = new SkiaSharp.SKPaint())
                {
                    paint.Typeface = SkiaSharp.SKTypeface.FromFamilyName(font);
                    paint.TextSize = fontSize;

                    // Measure each word and simulate line wrapping
                    var words = text.Split(' ');
                    float currentLineWidth = 0;
                    float totalHeight = fontSize * lineSpacing; // Start with one line
                    float maxWidth = 0;
                    bool isFirstWordOnLine = true;

                    foreach (var word in words)
                    {
                        // Measure word without trailing space first
                        float wordWidth = paint.MeasureText(word);
                        float spaceWidth = paint.MeasureText(" ");

                        // Check if we need to add space before this word (not first word on line)
                        float widthToAdd = isFirstWordOnLine ? wordWidth : (spaceWidth + wordWidth);

                        if (currentLineWidth + widthToAdd > constrainedWidth && !isFirstWordOnLine)
                        {
                            // Word doesn't fit, start new line
                            maxWidth = Math.Max(maxWidth, currentLineWidth);
                            currentLineWidth = wordWidth;
                            totalHeight += fontSize * lineSpacing;
                            isFirstWordOnLine = true; // Reset for new line
                        }
                        else
                        {
                            currentLineWidth += widthToAdd;
                            isFirstWordOnLine = false;
                        }
                    }

                    maxWidth = Math.Max(maxWidth, currentLineWidth);

                    return new Size(maxWidth, totalHeight);
                }
            }
            catch
            {
                // If measurement fails, return a safe default
                // Rough estimate: height = fontSize * lineSpacing * 2 (for 2 lines)
                return new Size(constrainedWidth, fontSize * lineSpacing * 2);
            }
        }

        /// <summary>
        /// Splits markdown text into small caps portion (first 4 words or until punctuation) and remaining text.
        /// </summary>
        private (string smallCapsText, string remainingText) SplitForSmallCaps(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return (string.Empty, string.Empty);

            // Strip markdown to work with plain text for word counting
            string plainText = StripMarkdownFormatting(markdown);

            // Split into words
            var words = new List<string>();
            var currentWord = new System.Text.StringBuilder();

            for (int i = 0; i < plainText.Length; i++)
            {
                char c = plainText[i];

                // Check for terminating punctuation (period, comma, quote)
                if (c == '.' || c == ',' || c == '"' || c == '\'' || c == '!' || c == '?')
                {
                    // Add current word if any
                    if (currentWord.Length > 0)
                    {
                        words.Add(currentWord.ToString());
                        currentWord.Clear();
                    }
                    // Include the punctuation with the last word
                    if (words.Count > 0)
                    {
                        words[words.Count - 1] += c;
                    }
                    // Stop here - found terminating punctuation
                    break;
                }
                else if (char.IsWhiteSpace(c))
                {
                    if (currentWord.Length > 0)
                    {
                        words.Add(currentWord.ToString());
                        currentWord.Clear();

                        // Stop if we have 4 words
                        if (words.Count >= 4)
                            break;
                    }
                }
                else
                {
                    currentWord.Append(c);
                }
            }

            // Add final word if any and haven't reached 4 yet
            if (currentWord.Length > 0 && words.Count < 4)
            {
                words.Add(currentWord.ToString());
            }

            if (words.Count == 0)
                return (string.Empty, markdown);

            // Build the small caps portion (original markdown for these words)
            string smallCapsPlainText = string.Join(" ", words);

            // Find where this ends in the original markdown
            int splitIndex = FindTextPositionInMarkdown(markdown, smallCapsPlainText);

            if (splitIndex > 0 && splitIndex < markdown.Length)
            {
                string smallCapsPortion = markdown.Substring(0, splitIndex).TrimEnd();
                string remaining = markdown.Substring(splitIndex).TrimStart();
                return (smallCapsPortion, remaining);
            }

            // Fallback: return everything as small caps if we can't split properly
            return (markdown, string.Empty);
        }

        /// <summary>
        /// Finds the position in markdown where the plain text ends, accounting for markdown formatting.
        /// </summary>
        private int FindTextPositionInMarkdown(string markdown, string plainText)
        {
            string strippedMarkdown = StripMarkdownFormatting(markdown);
            int plainIndex = strippedMarkdown.IndexOf(plainText);

            if (plainIndex < 0)
                return -1;

            int targetLength = plainIndex + plainText.Length;

            // Walk through markdown counting non-formatting characters
            int markdownIndex = 0;
            int plainCount = 0;

            while (markdownIndex < markdown.Length && plainCount < targetLength)
            {
                // Check if we're at a markdown formatting character
                if (markdownIndex + 1 < markdown.Length)
                {
                    string twoChar = markdown.Substring(markdownIndex, 2);
                    if (twoChar == "**" || twoChar == "__" || twoChar == "++")
                    {
                        markdownIndex += 2;
                        continue;
                    }
                }

                if (markdown[markdownIndex] == '*' || markdown[markdownIndex] == '_' || markdown[markdownIndex] == '+')
                {
                    markdownIndex++;
                    continue;
                }

                // Regular character
                plainCount++;
                markdownIndex++;
            }

            // Skip any trailing whitespace and get past the last word
            while (markdownIndex < markdown.Length && char.IsWhiteSpace(markdown[markdownIndex]))
            {
                markdownIndex++;
            }

            return markdownIndex;
        }

        /// <summary>
        /// Renders markdown text with small caps styling for first N words.
        /// </summary>
        private void RenderMarkdownWithSmallCaps(TextDescriptor textDescriptor, string markdown)
        {
            var (smallCapsText, remainingText) = SplitForSmallCaps(markdown);

            if (!string.IsNullOrEmpty(smallCapsText))
            {
                // Render small caps portion - uppercase with slightly smaller font
                string upperText = StripMarkdownFormatting(smallCapsText).ToUpper();
                textDescriptor.Span(upperText)
                    .FontFamily(_theme.BodyFont)
                    .FontSize(_theme.BodyFontSize * 0.85f)  // Slightly smaller for small caps effect
                    .LineHeight((float)_theme.LineSpacing);
            }

            if (!string.IsNullOrEmpty(remainingText))
            {
                // Add space between small caps and remaining text
                if (!string.IsNullOrEmpty(smallCapsText))
                    textDescriptor.Span(" ");

                // Render remaining text normally
                RenderMarkdownInlines(textDescriptor, remainingText);
            }
        }
    }
}