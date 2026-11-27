using System.Net.Http.Headers;
using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Auth;
using HamzetWasl.Application.Contracts.Social;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace HamzetWasl.IntegrationTests;

[Collection("Api")]
public class RestrictedCommunitiesTests(ApiFixture fixture)
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
    public async Task Disability_communities_belong_to_disabled_users_only()
    {
        var admin = Authed((await AdminLogin()).AccessToken);
        var du = Authed((await Register("disabled_user", "Member " + Suffix, Email("rm"), Phone())).AccessToken);
        var vol = Authed((await Register("volunteer", "Visitor " + Suffix, Email("rv"), Phone())).AccessToken);

        // Bad audience value is rejected.
        var (sBadAudience, _, _) = await admin.PostAsync<CommunityDto>("/api/v1/social/communities",
            new { name = "مجتمع " + Suffix, description = "وصف", rules = "قواعد", audienceRole = "aliens" });
        Assert.Equal(400, sBadAudience);

        // One community per disability type, restricted to disabled users.
        var (_, visual, _) = await admin.PostAsync<CommunityDto>("/api/v1/social/communities",
            new { name = "الإعاقة البصرية " + Suffix, description = "مجتمع الإعاقة البصرية", rules = "قواعد", audienceRole = "disabled_user" });
        Assert.NotNull(visual);
        Assert.Equal("disabled_user", visual!.AudienceRole);

        // And an open one for contrast.
        var (_, open, _) = await admin.PostAsync<CommunityDto>("/api/v1/social/communities",
            new { name = "عام " + Suffix, description = "مجتمع عام", rules = "قواعد" });
        Assert.NotNull(open);
        Assert.Null(open!.AudienceRole);

        // The volunteer does not see the restricted community; the disabled user and the admin do.
        var (_, volComms, _) = await vol.GetAsync<List<CommunityDto>>("/api/v1/social/communities");
        Assert.DoesNotContain(volComms!, c => c.CommunityId == visual.CommunityId);
        Assert.Contains(volComms!, c => c.CommunityId == open.CommunityId);
        var (_, duComms, _) = await du.GetAsync<List<CommunityDto>>("/api/v1/social/communities");
        Assert.Contains(duComms!, c => c.CommunityId == visual.CommunityId);
        var (_, adminComms, _) = await admin.GetAsync<List<CommunityDto>>("/api/v1/social/communities");
        Assert.Contains(adminComms!, c => c.CommunityId == visual.CommunityId);

        // Only the disabled user can join it.
        var (sVolJoin, _, _) = await vol.PostAsync<object?>($"/api/v1/social/communities/{visual.CommunityId}/join");
        Assert.Equal(403, sVolJoin);
        var (sDuJoin, _, _) = await du.PostAsync<object?>($"/api/v1/social/communities/{visual.CommunityId}/join");
        Assert.Equal(204, sDuJoin);

        // The disabled user posts; the volunteer cannot reach the post in any way.
        var (_, post, _) = await du.PostAsync<PostDto>("/api/v1/social/posts", new { communityId = visual.CommunityId, body = "أهلاً بيكم" });
        Assert.NotNull(post);

        var (_, volFeed, _) = await vol.GetAsync<PagedResult<PostDto>>("/api/v1/social/posts");
        Assert.DoesNotContain(volFeed!.Items, p => p.PostId == post!.PostId);
        var (_, duFeed, _) = await du.GetAsync<PagedResult<PostDto>>("/api/v1/social/posts");
        Assert.Contains(duFeed!.Items, p => p.PostId == post.PostId);

        var (sVolGet, _, _) = await vol.GetAsync<PostDetailDto>($"/api/v1/social/posts/{post.PostId}");
        Assert.Equal(404, sVolGet);
        var (sVolComment, _, _) = await vol.PostAsync<object?>($"/api/v1/social/posts/{post.PostId}/comments", new { body = "تطفل" });
        Assert.Equal(404, sVolComment);
        var (sVolReact, _, _) = await vol.PutAsync<object?>($"/api/v1/social/posts/{post.PostId}/reaction", new { type = "like" });
        Assert.Equal(404, sVolReact);
        var (sVolReport, _, _) = await vol.PostAsync<object?>($"/api/v1/social/posts/{post.PostId}/report", new { reason = "x" });
        Assert.Equal(404, sVolReport);

        // The open community still works for everyone.
        var (sOpenJoin, _, _) = await vol.PostAsync<object?>($"/api/v1/social/communities/{open.CommunityId}/join");
        Assert.Equal(204, sOpenJoin);
    }
}
