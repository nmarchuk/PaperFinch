using QuestPDF.Elements;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly List<string> _paragraphs;
        private readonly Models.PdfTheme _theme;

        public AlternatingMarginContent(string chapterTitle, string chapterSubtitle, string content, Models.PdfTheme theme)
        {
            _chapterTitle = chapterTitle;
            _chapterSubtitle = chapterSubtitle;
            _theme = theme;

            // Prefer splitting on two-or-more newlines (paragraph separators).
            // If that produces a single paragraph but the input contains single newlines,
            // fall back to splitting on single newlines so inputs that use single newlines
            // as paragraph boundaries are handled.
            var doubleSplit = Regex.Split(content, @"\r?\n\s*\r?\n")
                                   .Select(p => p.Trim())
                                   .Where(p => !string.IsNullOrWhiteSpace(p))
                                   .ToList();

            if (doubleSplit.Count > 1)
            {
                _paragraphs = doubleSplit;
            }
            else
            {
                // No double-newline paragraph separators detected.
                // If input contains single newlines, split on single lines; otherwise keep whole content.
                if (content.Contains('\n'))
                {
                    _paragraphs = Regex.Split(content, @"\r?\n")
                                       .Select(p => p.Trim())
                                       .Where(p => !string.IsNullOrWhiteSpace(p))
                                       .ToList();
                }
                else
                {
                    _paragraphs = new List<string>();
                    if (!string.IsNullOrWhiteSpace(content))
                        _paragraphs.Add(content.Trim());
                }
            }
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
            var isOdd = context.PageNumber % 2 == 1;
            var state = State;

            float availHeight = Convert.ToSingle(context.AvailableSize.Height);
            float availWidth = Convert.ToSingle(context.AvailableSize.Width);

            float leftMargin = isOdd ? (float)_theme.OutsideMargin : (float)_theme.InsideMargin;
            float rightMargin = isOdd ? (float)_theme.InsideMargin : (float)_theme.OutsideMargin;

            // Helper: build a column element from fragments and measure its total height (constrained width).
            float MeasureFragmentsHeight(IEnumerable<(string Type, string Text, bool Indent)> fragments)
            {
                var temp = context.CreateElement(e =>
                {
                    e.Width(availWidth, Unit.Point)
                     .PaddingLeft(leftMargin, Unit.Inch)
                     .PaddingRight(rightMargin, Unit.Inch)
                     .Column(col =>
                     {
                         foreach (var f in fragments)
                         {
                             switch (f.Type)
                             {
                                 case "Title":
                                     col.Item().PaddingBottom(10).Text(t =>
                                     {
                                         var s = t.Span(f.Text);
                                         s.FontFamily(_theme.ChapterTitleFont);
                                         s.FontSize(_theme.ChapterTitleFontSize);
                                         if (_theme.ChapterTitleBold) s.SemiBold();
                                         if (_theme.ChapterTitleItalic) s.Italic();
                                         ApplyAlignment(t, _theme.ChapterTitleAlignment);
                                     });
                                     break;
                                 case "Subtitle":
                                     col.Item().PaddingBottom(20).Text(t =>
                                     {
                                         var s = t.Span(f.Text);
                                         s.FontFamily(_theme.ChapterSubtitleFont);
                                         s.FontSize(_theme.ChapterSubtitleFontSize);
                                         if (_theme.ChapterSubtitleBold) s.SemiBold();
                                         if (_theme.ChapterSubtitleItalic) s.Italic();
                                         ApplyAlignment(t, _theme.ChapterSubtitleAlignment);
                                     });
                                     break;
                                 case "Para":
                                 case "Chunk":
                                     col.Item().PaddingBottom(8).Text(t =>
                                     {
                                         // Apply first-line indent using non-breaking spaces for indented paragraphs
                                         if (f.Indent)
                                         {
                                             // Use non-breaking spaces which won't be trimmed
                                             // Calculate number needed based on indent size
                                             int numSpaces = (int)Math.Ceiling(_theme.ParagraphIndent * 8);
                                             string indentSpaces = new string('\u00A0', numSpaces);
                                             t.Span(indentSpaces + f.Text).FontFamily(_theme.BodyFont).FontSize(_theme.BodyFontSize);
                                         }
                                         else
                                         {
                                             t.Span(f.Text).FontFamily(_theme.BodyFont).FontSize(_theme.BodyFontSize);
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
            var fragments = new List<(string Type, string Text, bool Indent)>();
            // Track measured total height of accepted fragments to compute incremental heights.
            float currentMeasuredTotal = 0f;
            float remaining = availHeight;

            // Title
            if (!state.TitleRendered && !string.IsNullOrWhiteSpace(_chapterTitle))
            {
                var candidate = ("Title", _chapterTitle, false);
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
                var candidate = ("Subtitle", _chapterSubtitle, false);
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
                var candidatePara = ("Para", full, shouldIndent);
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
                int low = 1, high = full.Length, best = 0;
                while (low <= high)
                {
                    int mid = (low + high) / 2;
                    string candidateText = TakeChunkAtWordBoundary(full, mid);
                    if (string.IsNullOrEmpty(candidateText))
                        candidateText = full.Substring(0, Math.Min(mid, full.Length));

                    var candidateChunk = ("Chunk", candidateText, shouldIndent);
                    float afterChunk = MeasureFragmentsHeight(fragments.Concat(new[] { candidateChunk }));
                    float incChunk = afterChunk - currentMeasuredTotal;

                    if (incChunk <= remaining)
                    {
                        best = candidateText.Length;
                        low = mid + 1;
                    }
                    else
                    {
                        high = mid - 1;
                    }
                }

                if (best > 0)
                {
                    // Found a chunk that fits
                    var chunkText = full.Substring(0, best);
                    var chunkCandidate = ("Chunk", chunkText, shouldIndent);
                    float afterChunk = MeasureFragmentsHeight(fragments.Concat(new[] { chunkCandidate }));
                    float incChunk = afterChunk - currentMeasuredTotal;

                    fragments.Add(chunkCandidate);
                    currentMeasuredTotal = afterChunk;

                    // Update character offset to track where we are in the paragraph
                    state.CharacterOffset += best;

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
                    var firstCandidate = ("Chunk", firstWord, shouldIndent);
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
                IContainer margin = element;
                margin = margin.PaddingLeft(leftMargin, Unit.Inch).PaddingRight(rightMargin, Unit.Inch);

                margin.Column(col =>
                {
                    foreach (var f in fragments)
                    {
                        switch (f.Type)
                        {
                            case "Title":
                                col.Item().PaddingBottom(10).Text(t =>
                                {
                                    var s = t.Span(f.Text);
                                    s.FontFamily(_theme.ChapterTitleFont);
                                    s.FontSize(_theme.ChapterTitleFontSize);
                                    if (_theme.ChapterTitleBold) s.SemiBold();
                                    if (_theme.ChapterTitleItalic) s.Italic();
                                    ApplyAlignment(t, _theme.ChapterTitleAlignment);
                                });
                                break;
                            case "Subtitle":
                                col.Item().PaddingBottom(20).Text(t =>
                                {
                                    var s = t.Span(f.Text);
                                    s.FontFamily(_theme.ChapterSubtitleFont);
                                    s.FontSize(_theme.ChapterSubtitleFontSize);
                                    if (_theme.ChapterSubtitleBold) s.SemiBold();
                                    if (_theme.ChapterSubtitleItalic) s.Italic();
                                    ApplyAlignment(t, _theme.ChapterSubtitleAlignment);
                                });
                                break;
                            case "Para":
                            case "Chunk":
                                col.Item().PaddingBottom(8).Text(t =>
                                {
                                    // Apply first-line indent using non-breaking spaces
                                    if (f.Indent)
                                    {
                                        // Use non-breaking spaces which won't be trimmed
                                        int numSpaces = (int)Math.Ceiling(_theme.ParagraphIndent * 8);
                                        string indentSpaces = new string('\u00A0', numSpaces);
                                        t.Span(indentSpaces + f.Text).FontFamily(_theme.BodyFont).FontSize(_theme.BodyFontSize);
                                    }
                                    else
                                    {
                                        t.Span(f.Text).FontFamily(_theme.BodyFont).FontSize(_theme.BodyFontSize);
                                    }
                                    t.Justify();
                                });
                                break;
                        }
                    }
                });
            });

            State = state;
            bool hasMore = state.ParagraphIndex < _paragraphs.Count;

            return new DynamicComponentComposeResult
            {
                Content = content,
                HasMoreContent = hasMore
            };
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
    }
}