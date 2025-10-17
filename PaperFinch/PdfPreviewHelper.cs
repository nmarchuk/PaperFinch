using Avalonia.Media.Imaging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;
using System;
using System.IO;

public static class PdfPreviewHelper
{
    public static IDocument DOCUMENT => Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.PageColor(QuestPDF.Helpers.Colors.White);
            page.DefaultTextStyle(x => x.FontSize(20));

            page.Header().Text("Sample PDF Preview").SemiBold().FontSize(36).FontColor(QuestPDF.Helpers.Colors.Blue.Medium);

            page.Content()
            .PaddingVertical(1, Unit.Centimetre)
            .Column(x =>
            {
                x.Spacing(20);

                x.Item().Text(Placeholders.LoremIpsum());
                x.Item().Image(Placeholders.Image(200, 100));
            });

            page.Footer()
                .AlignCenter()
                .Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                });
        });
    });

}
