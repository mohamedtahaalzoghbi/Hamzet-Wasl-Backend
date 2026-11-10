using System.Collections.Concurrent;
using HamzetWasl.Application.Interfaces;

namespace HamzetWasl.IntegrationTests;

/// <summary>
/// Records every notification the API publishes (and forwards it to the real SignalR
/// publisher), so the tests can assert the contracted payload.
/// </summary>
public sealed class RecordingNotificationPublisher(INotificationPublisher inner) : INotificationPublisher
{
    private readonly ConcurrentQueue<NotificationPayload> _published = new();

    public IReadOnlyList<NotificationPayload> Published => _published.ToArray();

    public NotificationPayload? Last => _published.LastOrDefault();

    public Task PublishAsync(Guid userId, NotificationPayload payload, CancellationToken ct = default)
    {
        _published.Enqueue(payload);
        return inner.PublishAsync(userId, payload, ct);
    }
}
