using DocIntel.Api.Auth;
using DocIntel.Api.Dtos;
using DocIntel.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocIntel.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/query")]
public class QueryController : ControllerBase
{
    private readonly IRagService _rag;

    public QueryController(IRagService rag) => _rag = rag;

    /// <summary>Ask a natural-language question answered with RAG over the workspace's documents.</summary>
    [HttpPost]
    public async Task<ActionResult<QueryResponse>> Ask(QueryRequest request, CancellationToken ct)
        => Ok(await _rag.QueryAsync(User.GetWorkspaceId(), request.Question, request.TopK, ct));
}
