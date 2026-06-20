using System.Text.Json;
using DocIntel.Api.Services;

namespace DocIntel.Api.Middleware;

/// <summary>Maps domain exceptions to clean JSON error responses.</summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            await WriteError(context, ex.StatusCode, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            await WriteError(context, 401, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteError(context, 500, "An unexpected error occurred.");
        }
    }

    private static async Task WriteError(HttpContext context, int status, string message)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        var payload = JsonSerializer.Serialize(new { error = message, status });
        await context.Response.WriteAsync(payload);
    }
}
