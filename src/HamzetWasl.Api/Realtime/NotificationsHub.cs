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
    private readonly IHubContext<NotificationsHub> _hub;

    public SignalRNotificationPublisher(IHubContext<NotificationsHub> hub) => _hub = hub;

    public Task PublishAsync(Guid userId, string notificationType, string channel, string? text, CancellationToken ct = default) =>
        _hub.Clients.Group($"user:{userId:N}").SendAsync("notification", new
        {
            type = notificationType,
            channel,
            text,
            createdAt = DateTime.UtcNow
        }, ct);
}
