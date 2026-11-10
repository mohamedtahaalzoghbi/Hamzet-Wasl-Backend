using HamzetWasl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace HamzetWasl.Api.Realtime;

[Authorize]
public sealed class NotificationsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? Context.User?.FindFirstValue("sub");

        if (Guid.TryParse(userId, out var id))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{id:N}");
        }

        await base.OnConnectedAsync();
    }
}

public sealed class SignalRNotificationPublisher : INotificationPublisher
{
    /// <summary>
    /// Client method name. It is part of the published contract
    /// (<c>docs/API_CONTRACT.md</c>, section 10) and must not change without a version bump.
    /// </summary>
    public const string ClientMethod = "notificationReceived";

    private readonly IHubContext<NotificationsHub> _hub;

    public SignalRNotificationPublisher(IHubContext<NotificationsHub> hub) => _hub = hub;

    public Task PublishAsync(Guid userId, NotificationPayload payload, CancellationToken ct = default) =>
        _hub.Clients.Group($"user:{userId:N}").SendAsync(ClientMethod, new
        {
            id = payload.Id,
            type = payload.Type,
            titleAr = payload.TitleAr,
            bodyAr = payload.BodyAr,
            createdAt = payload.CreatedAt,
            payloadJson = payload.PayloadJson
        }, ct);
}
