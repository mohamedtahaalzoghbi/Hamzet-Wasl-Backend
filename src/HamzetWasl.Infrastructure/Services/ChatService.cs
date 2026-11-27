using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Chat;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Domain.Constants;
using HamzetWasl.Domain.Entities;
using HamzetWasl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HamzetWasl.Infrastructure.Services;

/// <summary>
/// Direct one-to-one chat between two accounts. A conversation is stored once per pair:
/// the two user ids are kept ordered (low &lt; high) so the unique index on the pair
/// prevents duplicate conversations regardless of who started it.
/// </summary>
public sealed class ChatService(
    HamzetWaslDbContext db,
    INotificationService notifications,
    IFileStorage storage,
    IClock clock) : IChatService
{
    private static AppException Invalid(string field, string message) =>
        AppException.Validation(new Dictionary<string, string[]> { [field] = [message] });

    private static (int, int) Clamp(int page, int pageSize) => (Math.Max(1, page), Math.Clamp(pageSize, 1, 50));

    private static Guid OtherSide(ChatConversation conversation, Guid userId) =>
        conversation.UserLowId == userId ? conversation.UserHighId : conversation.UserLowId;

    public async Task<IReadOnlyList<ChatConversationDto>> GetConversationsAsync(Guid userId, CancellationToken ct = default)
    {
        var conversations = await db.ChatConversations
            .Where(c => c.UserLowId == userId || c.UserHighId == userId)
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        return await ToDtosAsync(conversations, userId, ct);
    }

    public async Task<ChatConversationDto> StartConversationAsync(Guid userId, Guid targetUserId, CancellationToken ct = default)
    {
        if (targetUserId == userId) throw Invalid("userId", "ما ينفعش تفتح محادثة مع نفسك.");
        var target = await db.UserAccounts.FirstOrDefaultAsync(u => u.UserId == targetUserId, ct)
                     ?? throw AppException.NotFound();
        if (target.AccountStatus != AccountStatuses.Active)
        {
            throw AppException.Business("CHAT_TARGET_UNAVAILABLE", "الحساب ده مش متاح للمحادثة حالياً.");
        }

        var (low, high) = string.CompareOrdinal(userId.ToString("N"), targetUserId.ToString("N")) < 0
            ? (userId, targetUserId)
            : (targetUserId, userId);

        var conversation = await db.ChatConversations
            .FirstOrDefaultAsync(c => c.UserLowId == low && c.UserHighId == high, ct);
        if (conversation is null)
        {
            conversation = new ChatConversation
            {
                ConversationId = Guid.NewGuid(),
                UserLowId = low,
                UserHighId = high,
                CreatedAt = clock.UtcNow
            };
            db.ChatConversations.Add(conversation);
            await db.SaveChangesAsync(ct);
        }

        return (await ToDtosAsync([conversation], userId, ct))[0];
    }

    public async Task<PagedResult<ChatMessageDto>> GetMessagesAsync(Guid userId, Guid conversationId, int page, int pageSize, CancellationToken ct = default)
    {
        await RequireMemberAsync(userId, conversationId, ct);
        (page, pageSize) = Clamp(page, pageSize);

        var query = db.ChatMessages.Where(m => m.ConversationId == conversationId);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(m => m.SentAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var names = await NamesAsync(rows.Select(m => m.SenderId), ct);
        var items = rows.Select(m => new ChatMessageDto(
            m.MessageId, m.SenderId, names.GetValueOrDefault(m.SenderId, "مستخدم"),
            m.Body, m.SentAt, m.ReadAt, m.SenderId == userId)).ToList();
        return new PagedResult<ChatMessageDto>(items, page, pageSize, total);
    }

    public async Task<ChatMessageDto> SendMessageAsync(Guid userId, Guid conversationId, string body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(body)) throw Invalid("body", "اكتب نص الرسالة.");
        if (body.Length > 2000) throw Invalid("body", "الرسالة طويلة (حتى 2000 حرف).");
        var conversation = await RequireMemberAsync(userId, conversationId, ct);

        var now = clock.UtcNow;
        var message = new ChatMessage
        {
            MessageId = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = userId,
            Body = body.Trim(),
            SentAt = now
        };
        db.ChatMessages.Add(message);
        conversation.LastMessageAt = now;
        await db.SaveChangesAsync(ct);

        var other = OtherSide(conversation, userId);
        await notifications.NotifyAsync(other, NotificationTypes.ChatMessageReceived,
            "وصلتك رسالة جديدة في الشات.", "chat_conversation", conversationId, ct);

        var names = await NamesAsync([userId], ct);
        return new ChatMessageDto(message.MessageId, userId, names.GetValueOrDefault(userId, "مستخدم"),
            message.Body, message.SentAt, null, true);
    }

    public async Task MarkReadAsync(Guid userId, Guid conversationId, CancellationToken ct = default)
    {
        var conversation = await RequireMemberAsync(userId, conversationId, ct);
        var other = OtherSide(conversation, userId);
        var now = clock.UtcNow;

        var unread = await db.ChatMessages
            .Where(m => m.ConversationId == conversationId && m.SenderId == other && m.ReadAt == null)
            .ToListAsync(ct);
        foreach (var message in unread) message.ReadAt = now;
        await db.SaveChangesAsync(ct);
    }

    private async Task<ChatConversation> RequireMemberAsync(Guid userId, Guid conversationId, CancellationToken ct)
    {
        var conversation = await db.ChatConversations
            .FirstOrDefaultAsync(c => c.ConversationId == conversationId, ct) ?? throw AppException.NotFound();
        if (conversation.UserLowId != userId && conversation.UserHighId != userId) throw AppException.NotFound();
        return conversation;
    }

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await db.UserProfiles.Where(p => list.Contains(p.UserId)).ToDictionaryAsync(p => p.UserId, p => p.DisplayName, ct);
    }

    private async Task<List<ChatConversationDto>> ToDtosAsync(List<ChatConversation> conversations, Guid userId, CancellationToken ct)
    {
        var otherIds = conversations.Select(c => OtherSide(c, userId)).Distinct().ToList();
        var profiles = await db.UserProfiles.Where(p => otherIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, ct);
        var roles = await (from u in db.UserAccounts
                           join r in db.AccountRoles on u.RoleId equals r.RoleId
                           where otherIds.Contains(u.UserId)
                           select new { u.UserId, r.RoleName }).ToDictionaryAsync(x => x.UserId, x => x.RoleName, ct);

        var conversationIds = conversations.Select(c => c.ConversationId).ToList();
        var lastMessages = await db.ChatMessages
            .Where(m => conversationIds.Contains(m.ConversationId))
            .GroupBy(m => m.ConversationId)
            .Select(g => g.OrderByDescending(m => m.SentAt).First())
            .ToDictionaryAsync(m => m.ConversationId, ct);
        var unread = await db.ChatMessages
            .Where(m => conversationIds.Contains(m.ConversationId) && m.SenderId != userId && m.ReadAt == null)
            .GroupBy(m => m.ConversationId)
            .Select(g => new { g.Key, N = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.N, ct);

        return conversations.Select(c =>
        {
            var otherId = OtherSide(c, userId);
            profiles.TryGetValue(otherId, out var profile);
            lastMessages.TryGetValue(c.ConversationId, out var last);
            var participant = new ChatParticipantDto(
                otherId,
                profile?.DisplayName ?? "مستخدم",
                roles.GetValueOrDefault(otherId, ""),
                string.IsNullOrWhiteSpace(profile?.AvatarStorageKey) ? null : storage.GetPublicUrl(profile.AvatarStorageKey));
            return new ChatConversationDto(
                c.ConversationId, participant, last?.Body, last?.SenderId, last?.SentAt ?? c.LastMessageAt,
                unread.GetValueOrDefault(c.ConversationId), c.CreatedAt);
        }).ToList();
    }
}
