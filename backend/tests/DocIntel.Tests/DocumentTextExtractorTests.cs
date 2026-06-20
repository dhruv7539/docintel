using System.Text;
using DocIntel.Api.Ingestion;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace DocIntel.Tests;

public class DocumentTextExtractorTests
{
    private readonly DocumentTextExtractor _extractor = new();

    [Fact]
    public async Task Extract_PlainText_ReturnsContentVerbatim()
    {
        const string text = "plain text about rollback";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

        var result = await _extractor.ExtractAsync(stream, "notes.txt", "text/plain");

        Assert.Equal(text, result);
    }

    [Fact]
    public async Task Extract_Pdf_ReturnsEmbeddedText()
    {
        var bytes = BuildPdf("Helm SDK auto-rollback brings MTTR down");
        using var stream = new MemoryStream(bytes);

        var result = await _extractor.ExtractAsync(stream, "report.pdf", "application/pdf");

        Assert.Contains("auto-rollback", result);
    }

    [Fact]
    public async Task Extract_Docx_ReturnsParagraphText()
    {
        var bytes = BuildDocx("Quarterly rollback summary");
        using var stream = new MemoryStream(bytes);

        var result = await _extractor.ExtractAsync(
            stream,
            "summary.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

        Assert.Contains("rollback summary", result);
    }

    private static byte[] BuildPdf(string text)
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        page.AddText(text, 12, new PdfPoint(25, 700), font);
        return builder.Build();
    }

    private static byte[] BuildDocx(string text)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body(new Paragraph(new Run(new Text(text)))));
        }

        return ms.ToArray();
    }
}
