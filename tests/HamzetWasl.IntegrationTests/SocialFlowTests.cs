using System.Net.Http.Headers;
using System.Text.Json;
using HamzetWasl.Application.Contracts.Admin;
using HamzetWasl.Application.Contracts.Auth;
using HamzetWasl.Application.Contracts.Social;
using HamzetWasl.Application.Contracts.Volunteer;
using HamzetWasl.Application.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace HamzetWasl.IntegrationTests;

[Collection("Api")]
public class SocialFlowTests(ApiFixture fixture)
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

    /// <summary>Registers a user through the two-step OTP flow and asserts it succeeded.</summary>
    private async Task<AuthResponse> Register(string role, string name, string email, string phone)
    {
        return await Client.RegisterVerifiedAsync(fixture.Messages, RegisterBody(role, name, email, phone));
    }

    /// <summary>Registers a user and asserts the call failed with the given status.</summary>
    private async Task<ApiError> RegisterFailing(
        string role, string name, string email, string phone, int expectedStatus)
    {
        var (status, _, error) =
            await Client.PostAsync<AuthResponse>("/api/v1/auth/register", RegisterBody(role, name, email, phone));
        Assert.Equal(expectedStatus, status);
        Assert.NotNull(error);
        return error!;
    }

    private static object RegisterBody(string role, string name, string email, string phone) => new
    {
        role,
        name = role is "association" or "company" ? null : name,
        organizationName = role is "association" or "company" ? name : null,
        email,
        phone,
        password = "Passw0rd!",
        confirmPassword = "Passw0rd!",
        termsAccepted = true,
        policyVersion = "1.0"
    };

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
    public async Task Social_timeline_lifecycle()
    {
        var admin = Authed((await AdminLogin()).AccessToken);
        var du = Authed((await Register("disabled_user", "Social One", Email("sa"), Phone())).AccessToken);
        var vol = Authed((await Register("volunteer", "Social Two", Email("sb"), Phone())).AccessToken);

        var (s0, _, _) = await du.PostAsync<CommunityDto>("/api/v1/social/communities", new { name = "x", description = "d", rules = "r" });
        Assert.Equal(403, s0);
        var (_, comm, e1) = await admin.PostAsync<CommunityDto>("/api/v1/social/communities", new { name = "مجتمع " + Suffix, description = "وصف", rules = "قواعد" });
        Assert.Null(e1);

        var (sNo, _, _) = await du.PostAsync<PostDto>("/api/v1/social/posts", new { communityId = comm!.CommunityId, body = "قبل الانضمام" });
        Assert.Equal(403, sNo);
        var (_, _, e2) = await du.PostAsync<object?>($"/api/v1/social/communities/{comm.CommunityId}/join");
        Assert.Null(e2);
        var (_, post, e3) = await du.PostAsync<PostDto>("/api/v1/social/posts", new { communityId = comm.CommunityId, body = "أول بوست" });
        Assert.Null(e3);

        var (_, feed, _) = await vol.GetAsync<PagedResult<PostDto>>("/api/v1/social/posts");
        Assert.Contains(feed!.Items, p => p.PostId == post!.PostId);

        var (_, _, e4) = await vol.PostAsync<object?>($"/api/v1/social/posts/{post!.PostId}/comments", new { body = "تعليق" });
        Assert.Null(e4);
        var (_, _, e5) = await vol.PutAsync<object?>($"/api/v1/social/posts/{post.PostId}/reaction", new { type = "like" });
        Assert.Null(e5);
        var (sBad, _, _) = await vol.PutAsync<object?>($"/api/v1/social/posts/{post.PostId}/reaction", new { type = "nope" });
        Assert.Equal(400, sBad);
        var (_, detail, _) = await vol.GetAsync<PostDetailDto>($"/api/v1/social/posts/{post.PostId}");
        Assert.Single(detail!.Comments);
        Assert.Equal(1, detail.Post.ReactionsCount);
        Assert.Equal("like", detail.Post.MyReaction);

        var (_, _, e6) = await vol.PutAsync<object?>($"/api/v1/social/users/{post.AuthorId}/follow");
        Assert.Null(e6);
        var (_, prof, _) = await vol.GetAsync<PublicProfileDto>($"/api/v1/social/users/{post.AuthorId}");
        Assert.True(prof!.IsFollowing);
        Assert.Equal(1, prof.FollowersCount);

        var (_, _, e7) = await vol.PostAsync<object?>($"/api/v1/social/posts/{post.PostId}/report", new { reason = "اختبار" });
        Assert.Null(e7);
        var (_, reports, _) = await admin.GetAsync<List<ContentReportDto>>("/api/v1/social/reports?status=open");
        var rep = reports!.First(r => r.PostId == post.PostId);
        var (_, _, e8) = await admin.PostAsync<object?>($"/api/v1/social/reports/{rep.ReportId}/resolve", new { hide = true, resolution = "مخالف" });
        Assert.Null(e8);
        var (sGone, _, _) = await vol.GetAsync<PostDetailDto>($"/api/v1/social/posts/{post.PostId}");
        Assert.Equal(404, sGone);
    }
}
