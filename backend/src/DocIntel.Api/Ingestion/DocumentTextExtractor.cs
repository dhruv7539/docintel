using System.Text;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace DocIntel.Api.Ingestion;

/// <summary>
/// Extracts plain text from an uploaded file so it can be chunked and embedded.
/// PDFs and Word (.docx) documents are parsed into text; everything else is read
/// as UTF-8 plain text (the default for .txt/.md and pasted content).
/// </summary>
public interface IDocumentTextExtractor
{
    Task<string> ExtractAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default);
}

public class DocumentTextExtractor : IDocumentTextExtractor
{
    public async Task<string> ExtractAsync(
        Stream stream, string fileName, string contentType, CancellationToken ct = default)
    {
        var kind = DetectKind(fileName, contentType);

        return kind switch
        {
            FileKind.Pdf => ExtractPdf(await BufferAsync(stream, ct)),
            FileKind.Docx => ExtractDocx(await BufferAsync(stream, ct)),
            _ => await ReadTextAsync(stream, ct)
        };
    }

    private enum FileKind { Text, Pdf, Docx }

    private static FileKind DetectKind(string fileName, string contentType)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext == ".pdf" || contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            return FileKind.Pdf;
        }

        if (ext == ".docx" ||
            contentType.Contains("officedocument.wordprocessingml", StringComparison.OrdinalIgnoreCase))
        {
            return FileKind.Docx;
        }

        return FileKind.Text;
    }

    private static async Task<byte[]> BufferAsync(Stream stream, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    private static async Task<string> ReadTextAsync(Stream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(ct);
    }

    private static string ExtractPdf(byte[] bytes)
    {
        using var pdf = PdfDocument.Open(bytes);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages())
        {
            sb.AppendLine(page.Text);
        }

        return sb.ToString().Trim();
    }

    private static string ExtractDocx(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        return body is null ? string.Empty : body.InnerText.Trim();
    }
}
