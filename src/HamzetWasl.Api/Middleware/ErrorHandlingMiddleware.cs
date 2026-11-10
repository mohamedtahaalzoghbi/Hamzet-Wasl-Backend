using System.Text.Json;
using HamzetWasl.Api.Serialization;
using HamzetWasl.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace HamzetWasl.Api.Middleware;

public sealed class TraceIdMiddleware
{
    private readonly RequestDelegate _next;

    public TraceIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Request-Id", out var requestId) &&
            !string.IsNullOrWhiteSpace(requestId))
        {
            context.TraceIdentifier = requestId.ToString();
        }

        context.Response.Headers["X-Trace-Id"] = context.TraceIdentifier;
        await _next(context);
    }
}

public sealed class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
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
            await WriteAsync(context, ex.StatusCode, ex.Code, ex.Message, ex.Fields);
        }
        catch (BadHttpRequestException ex)
        {
            // Kestrel rejects a body that exceeds the request limit before the action runs.
            // The contract documents 413 REQUEST_TOO_LARGE for that case.
            if (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
            {
                await WriteAsync(context, StatusCodes.Status413PayloadTooLarge,
                    ErrorCodes.RequestTooLarge, ArabicMessages.For(ErrorCodes.RequestTooLarge), null);
                return;
            }

            _logger.LogWarning(ex, "Rejected request. TraceId={TraceId}", context.TraceIdentifier);
            await WriteAsync(context, ex.StatusCode, ErrorCodes.ValidationError,
                ArabicMessages.For(ErrorCodes.ValidationError), null);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Database update failed. TraceId={TraceId}", context.TraceIdentifier);
            await WriteAsync(context, 409, ErrorCodes.Conflict, ArabicMessages.For(ErrorCodes.Conflict), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception. TraceId={TraceId}", context.TraceIdentifier);
            await WriteAsync(context, 500, ErrorCodes.InternalError, ArabicMessages.For(ErrorCodes.InternalError), null);
        }
    }

    private static async Task WriteAsync(
        HttpContext context,
        int statusCode,
        string code,
        string message,
        IReadOnlyDictionary<string, string[]>? fields)
    {
        if (context.Response.HasStarted) return;
        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        var response = ApiResponse<object?>.Fail(new ApiError(code, message, fields), context.TraceIdentifier);
        await JsonSerializer.SerializeAsync(context.Response.Body, response, JsonDefaults.Options, context.RequestAborted);
    }
}
