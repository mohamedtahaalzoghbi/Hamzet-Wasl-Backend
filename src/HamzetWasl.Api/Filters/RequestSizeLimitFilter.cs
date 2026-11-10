using HamzetWasl.Api.Serialization;
using HamzetWasl.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HamzetWasl.Api.Filters;

/// <summary>
/// Enforces the request size limit of an endpoint in the application layer.
/// <para>
/// Kestrel enforces the limit at the transport level, but a request can also arrive through
/// a host that does not (or behind a proxy with a different limit). Checking the declared
/// content length keeps the behaviour identical everywhere and answers the documented
/// 413 REQUEST_TOO_LARGE instead of a framework error.
/// </para>
/// </summary>
public sealed class RequestSizeLimitFilter : IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var limit = context.ActionDescriptor.EndpointMetadata
            .OfType<Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata>()
            .Select(metadata => metadata.MaxRequestBodySize)
            .FirstOrDefault();

        if (limit is null)
        {
            return;
        }

        var contentLength = context.HttpContext.Request.ContentLength;
        if (contentLength is null || contentLength <= 0 || contentLength <= limit)
        {
            return;
        }

        var response = ApiResponse<object?>.Fail(
            new ApiError(ErrorCodes.RequestTooLarge, ArabicMessages.For(ErrorCodes.RequestTooLarge), null),
            context.HttpContext.TraceIdentifier);

        context.Result = new ObjectResult(response) { StatusCode = StatusCodes.Status413PayloadTooLarge };
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
