namespace HamzetWasl.Application.Contracts.Chat;

public sealed record StartConversationRequest(Guid UserId);

public sealed record SendChatMessageRequest(string Body);

/// <summary>The other side of a direct conversation, as the caller sees it.</summary>
public sealed record ChatParticipantDto(Guid UserId, string DisplayName, string Role, string? AvatarUrl);

public sealed record ChatConversationDto(
    Guid ConversationId,
    ChatParticipantDto Other,
    string? LastMessageBody,
    Guid? LastMessageSenderId,
    DateTime? LastMessageAt,
    int UnreadCount,
    DateTime CreatedAt);

public sealed record ChatMessageDto(
    Guid MessageId,
    Guid SenderId,
    string SenderName,
    string Body,
    DateTime SentAt,
    DateTime? ReadAt,
    bool IsMine);
