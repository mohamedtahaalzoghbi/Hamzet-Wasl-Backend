using System.Security.Claims;
using HamzetWasl.Api.Serialization;
using HamzetWasl.Application.Common;
using HamzetWasl.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace HamzetWasl.Api.Middleware;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    public Guid? UserId
    {
        get
        {
            var value = _accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? _accessor.HttpContext?.User.FindFirstValue("sub");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Role => _accessor.HttpContext?.User.FindFirstValue("role");
    public bool IsAuthenticated => _accessor.HttpContext?.User.Identity?.IsAuthenticated == true;
    public string TraceId => _accessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
}

/// <summary>Blocks a still-valid access token after an admin suspends the account.</summary>
public sealed class SuspendedUserMiddleware
{
    private readonly RequestDelegate _next;

    public SuspendedUserMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, HamzetWasl.Infrastructure.Persistence.HamzetWaslDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            !context.Request.Path.StartsWithSegments("/health") &&
            !context.Request.Path.StartsWithSegments("/swagger"))
        {
            var value = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
            if (Guid.TryParse(value, out var userId))
            {
                var status = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                    .FirstOrDefaultAsync(db.UserAccounts.Where(a => a.UserId == userId)
                        .Select(a => a.AccountStatus), context.RequestAborted);

                if (status == HamzetWasl.Domain.Constants.AccountStatuses.Suspended &&
                    !context.Request.Path.StartsWithSegments("/api/v1/auth/logout"))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json; charset=utf-8";
                    var response = ApiResponse<object?>.Fail(
                        new ApiError(ErrorCodes.AccountSuspended, ArabicMessages.For(ErrorCodes.AccountSuspended)),
                        context.TraceIdentifier);
                    await System.Text.Json.JsonSerializer.SerializeAsync(context.Response.Body, response,
                        JsonDefaults.Options, context.RequestAborted);
                    return;
                }
            }
        }

        await _next(context);
    }
}
