using System.Net.Http.Headers;
using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Auth;
using HamzetWasl.Application.Contracts.Chat;
using HamzetWasl.Domain.Constants;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace HamzetWasl.IntegrationTests;

[Collection("Api")]
public class ChatFlowTests(ApiFixture fixture)
{
    private HttpClient Client => fixture.AnonymousClient;
    private string Suffix { get; } = Guid.NewGuid().ToString("N")[..10];

    private HttpClient Authed(string accessToken)
    {
        var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private string Email(string prefix) => $"{prefix}{Suffix}@example.com";
    private string Phone()
    {
        var ticks = DateTime.UtcNow.Ticks % 100_000_000;
        return $"010{ticks:D8}";
    }

    private async Task<AuthResponse> Register(string role, string name, string email, string phone)
    {
        return await Client.RegisterVerifiedAsync(fixture.Messages, new
        {
            role,
            name,
            organizationName = (string?)null,
            email,
            phone,
            password = "Passw0rd!",
            confirmPassword = "Passw0rd!",
            termsAccepted = true,
            policyVersion = "1.0"
        });
    }

    private async Task<AuthResponse> AdminLogin()
    {
        var (status, data, _) = await Client.PostAsync<AuthResponse>("/api/v1/auth/login", new
        {
            emailOrPhone = "admin@hamzetwasl.local",
            password = "Admin@12345"
        });

        Assert.Equal(200, status);
        Assert.NotNull(data);
        return data!;
    }

    [Fact]
    public async Task Direct_chat_lifecycle()
    {
        var aAuth = await Register("disabled_user", "Chat One", Email("ca"), Phone());
        var a = Authed(aAuth.AccessToken);
        var bAuth = await Register("volunteer", "Chat Two", Email("cb"), Phone());
        var b = Authed(bAuth.AccessToken);

        // Talking to yourself is rejected, and so is a user who does not exist.
        var (sSelf, _, _) = await a.PostAsync<ChatConversationDto>("/api/v1/chat/conversations", new { userId = aAuth.UserId });
        Assert.Equal(400, sSelf);
        var (sGhost, _, _) = await a.PostAsync<ChatConversationDto>("/api/v1/chat/conversations", new { userId = Guid.Empty });
        Assert.Equal(404, sGhost);

        var (sConv, conv, eConv) = await a.PostAsync<ChatConversationDto>("/api/v1/chat/conversations", new { userId = bAuth.UserId });
        Assert.Null(eConv);
        Assert.Equal(200, sConv);
        Assert.Equal(bAuth.UserId, conv!.Other.UserId);
        Assert.Equal("Chat Two", conv.Other.DisplayName);
        Assert.Equal("volunteer", conv.Other.Role);
        Assert.Equal(0, conv.UnreadCount);

        // Opening the same pair again returns the same conversation.
        var (_, convAgain, _) = await a.PostAsync<ChatConversationDto>("/api/v1/chat/conversations", new { userId = bAuth.UserId });
        Assert.Equal(conv.ConversationId, convAgain!.ConversationId);

        var (sEmpty, _, _) = await a.PostAsync<ChatMessageDto>($"/api/v1/chat/conversations/{conv.ConversationId}/messages", new { body = " " });
        Assert.Equal(400, sEmpty);

        var (sMsg, msg, eMsg) = await a.PostAsync<ChatMessageDto>($"/api/v1/chat/conversations/{conv.ConversationId}/messages", new { body = "أهلاً، محتاج مساعدة" });
        Assert.Null(eMsg);
        Assert.Equal(201, sMsg);
        Assert.True(msg!.IsMine);
        Assert.Null(msg.ReadAt);

        // The other side got a chat notification.
        var recorder = (RecordingNotificationPublisher)fixture.Factory.Services.GetService(typeof(HamzetWasl.Application.Interfaces.INotificationPublisher))!;
        Assert.Contains(recorder.Published, p => p.Type == NotificationTypes.ChatMessageReceived);

        // B sees the conversation with one unread message.
        var (_, bConvs, _) = await b.GetAsync<List<ChatConversationDto>>("/api/v1/chat/conversations");
        var bConv = Assert.Single(bConvs!);
        Assert.Equal(conv.ConversationId, bConv.ConversationId);
        Assert.Equal(1, bConv.UnreadCount);
        Assert.Equal("أهلاً، محتاج مساعدة", bConv.LastMessageBody);

        var (_, bMsgs, _) = await b.GetAsync<PagedResult<ChatMessageDto>>($"/api/v1/chat/conversations/{conv.ConversationId}/messages");
        var bMsg = Assert.Single(bMsgs!.Items);
        Assert.False(bMsg.IsMine);
        Assert.Equal("Chat One", bMsg.SenderName);

        // Reading clears the unread badge and timestamps the message.
        var (sRead, _, _) = await b.PutAsync<object?>($"/api/v1/chat/conversations/{conv.ConversationId}/read");
        Assert.Equal(204, sRead);
        var (_, bConvsAfter, _) = await b.GetAsync<List<ChatConversationDto>>("/api/v1/chat/conversations");
        Assert.Equal(0, Assert.Single(bConvsAfter!).UnreadCount);
        var (_, aMsgs, _) = await a.GetAsync<PagedResult<ChatMessageDto>>($"/api/v1/chat/conversations/{conv.ConversationId}/messages");
        Assert.NotNull(Assert.Single(aMsgs!.Items).ReadAt);

        // The reply flips the unread count to A.
        var (_, reply, _) = await b.PostAsync<ChatMessageDto>($"/api/v1/chat/conversations/{conv.ConversationId}/messages", new { body = "تحت أمرك" });
        Assert.NotNull(reply);
        var (_, aConvs, _) = await a.GetAsync<List<ChatConversationDto>>("/api/v1/chat/conversations");
        Assert.Equal(1, Assert.Single(aConvs!).UnreadCount);
    }

    [Fact]
    public async Task Chat_is_closed_to_strangers_and_admins()
    {
        var a = Authed((await Register("disabled_user", "Chat Three", Email("cc"), Phone())).AccessToken);
        var bAuth = await Register("volunteer", "Chat Four", Email("cd"), Phone());
        var c = Authed((await Register("volunteer", "Chat Five", Email("ce"), Phone())).AccessToken);
        var admin = Authed((await AdminLogin()).AccessToken);

        var (_, conv, _) = await a.PostAsync<ChatConversationDto>("/api/v1/chat/conversations", new { userId = bAuth.UserId });
        Assert.NotNull(conv);

        var (sList, _, _) = await c.GetAsync<PagedResult<ChatMessageDto>>($"/api/v1/chat/conversations/{conv!.ConversationId}/messages");
        Assert.Equal(404, sList);
        var (sSend, _, _) = await c.PostAsync<ChatMessageDto>($"/api/v1/chat/conversations/{conv.ConversationId}/messages", new { body = "تطفل" });
        Assert.Equal(404, sSend);
        var (sRead, _, _) = await c.PutAsync<object?>($"/api/v1/chat/conversations/{conv.ConversationId}/read");
        Assert.Equal(404, sRead);

        // The admin moderates but does not chat.
        var (sAdmin, _, _) = await admin.GetAsync<List<ChatConversationDto>>("/api/v1/chat/conversations");
        Assert.Equal(403, sAdmin);
    }
}
