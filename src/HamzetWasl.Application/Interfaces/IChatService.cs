using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Chat;

namespace HamzetWasl.Application.Interfaces;

public interface IChatService
{
    /// <summary>The caller's conversations, the one with the newest message first.</summary>
    Task<IReadOnlyList<ChatConversationDto>> GetConversationsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Returns the existing direct conversation with the target user, or creates it.</summary>
    Task<ChatConversationDto> StartConversationAsync(Guid userId, Guid targetUserId, CancellationToken ct = default);

    /// <summary>Messages of a conversation the caller belongs to, newest first.</summary>
    Task<PagedResult<ChatMessageDto>> GetMessagesAsync(Guid userId, Guid conversationId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Sends a text message and notifies the other side.</summary>
    Task<ChatMessageDto> SendMessageAsync(Guid userId, Guid conversationId, string body, CancellationToken ct = default);

    /// <summary>Marks every message from the other side as read; idempotent.</summary>
    Task MarkReadAsync(Guid userId, Guid conversationId, CancellationToken ct = default);
}
