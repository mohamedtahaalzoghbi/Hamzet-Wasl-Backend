using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Chat;
using HamzetWasl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HamzetWasl.Api.Controllers;

[Authorize(Policy = "Member")]
[Route("api/v1/chat")]
public sealed class ChatController(IChatService chat) : ApiControllerBase
{
    /// <summary>[Bearer Token] The caller's conversations with the other side, last message and unread count.</summary>
    [HttpGet("conversations")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ChatConversationDto>>>> Conversations(CancellationToken ct) =>
        Ok(Envelope(await chat.GetConversationsAsync(CurrentUserId, ct)));

    /// <summary>[Bearer Token] Opens the direct conversation with a user, creating it on first contact.</summary>
    [HttpPost("conversations")]
    public async Task<ActionResult<ApiResponse<ChatConversationDto>>> Start([FromBody] StartConversationRequest request, CancellationToken ct) =>
        Ok(Envelope(await chat.StartConversationAsync(CurrentUserId, request.UserId, ct)));

    /// <summary>[Bearer Token] Messages of a conversation, newest first.</summary>
    [HttpGet("conversations/{id:guid}/messages")]
    public async Task<ActionResult<ApiResponse<PagedResult<ChatMessageDto>>>> Messages(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(Envelope(await chat.GetMessagesAsync(CurrentUserId, id, page, pageSize, ct)));

    /// <summary>[Bearer Token] Sends a text message; the other side gets a chat_message_received notification.</summary>
    [HttpPost("conversations/{id:guid}/messages")]
    public async Task<ActionResult<ApiResponse<ChatMessageDto>>> Send(Guid id, [FromBody] SendChatMessageRequest request, CancellationToken ct) =>
        CreatedEnvelope(await chat.SendMessageAsync(CurrentUserId, id, request.Body, ct));

    /// <summary>[Bearer Token] Marks everything from the other side as read.</summary>
    [HttpPut("conversations/{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct) { await chat.MarkReadAsync(CurrentUserId, id, ct); return NoContent(); }
}
