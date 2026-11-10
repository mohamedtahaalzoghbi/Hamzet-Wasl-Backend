using System.Security.Claims;
using HamzetWasl.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace HamzetWasl.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected string TraceId => HttpContext.TraceIdentifier;

    protected Guid CurrentUserId
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (!Guid.TryParse(value, out var id))
            {
                throw AppException.Unauthorized();
            }

            return id;
        }
    }

    protected string? CurrentRole => User.FindFirstValue("role");

    protected ApiResponse<T> Envelope<T>(T data) => ApiResponse<T>.Ok(data, TraceId);

    protected ObjectResult CreatedEnvelope<T>(T data) =>
        StatusCode(StatusCodes.Status201Created, Envelope(data));
}
