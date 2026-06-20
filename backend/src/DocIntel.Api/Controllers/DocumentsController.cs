using DocIntel.Api.Auth;
using DocIntel.Api.Dtos;
using DocIntel.Api.Ingestion;
using DocIntel.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocIntel.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documents;
    private readonly IDocumentTextExtractor _extractor;

    public DocumentsController(IDocumentService documents, IDocumentTextExtractor extractor)
    {
        _documents = documents;
        _extractor = extractor;
    }

    /// <summary>Upload a file (multipart/form-data) for indexing. PDF, .docx, and plain text are supported.</summary>
    [HttpPost]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<DocumentDto>> Upload(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "A non-empty file is required." });
        }

        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "text/plain" : file.ContentType;

        string content;
        await using (var stream = file.OpenReadStream())
        {
            content = await _extractor.ExtractAsync(stream, file.FileName, contentType, ct);
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return BadRequest(new { error = "No readable text could be extracted from the file." });
        }

        var dto = await _documents.IngestTextAsync(
            User.GetWorkspaceId(),
            file.FileName,
            content,
            contentType,
            ct);

        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    /// <summary>Upload raw text as JSON (handy for the demo UI and tests).</summary>
    [HttpPost("text")]
    public async Task<ActionResult<DocumentDto>> UploadText(UploadTextRequest request, CancellationToken ct)
    {
        var dto = await _documents.IngestTextAsync(
            User.GetWorkspaceId(), request.FileName, request.Content, "text/plain", ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> List(CancellationToken ct)
        => Ok(await _documents.ListAsync(User.GetWorkspaceId(), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _documents.GetAsync(User.GetWorkspaceId(), id, ct));
}
