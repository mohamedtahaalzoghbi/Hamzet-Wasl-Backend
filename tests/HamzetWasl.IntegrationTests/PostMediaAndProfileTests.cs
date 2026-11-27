using System.Net.Http.Headers;
using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Auth;
using HamzetWasl.Application.Contracts.Social;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace HamzetWasl.IntegrationTests;

[Collection("Api")]
public class PostMediaAndProfileTests(ApiFixture fixture)
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

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
    public async Task Post_images_appear_in_the_feed()
    {
        var admin = Authed((await AdminLogin()).AccessToken);
        var author = Authed((await Register("volunteer", "Poster " + Suffix, Email("pm"), Phone())).AccessToken);
        var reader = Authed((await Register("disabled_user", "Reader " + Suffix, Email("pr"), Phone())).AccessToken);

        var (_, comm, _) = await admin.PostAsync<CommunityDto>("/api/v1/social/communities",
            new { name = "صور " + Suffix, description = "وصف", rules = "قواعد" });
        Assert.NotNull(comm);
        await author.PostAsync<object?>($"/api/v1/social/communities/{comm!.CommunityId}/join");
        var (_, post, _) = await author.PostAsync<PostDto>("/api/v1/social/posts", new { communityId = comm.CommunityId, body = "بوست بصور" });
        Assert.NotNull(post);
        Assert.Empty(post!.Media);

        // Alt text is mandatory: it is what screen readers read for the image.
        var (sNoAlt, _, _) = await author.UploadFormAsync<PostMediaDto>(
            $"/api/v1/social/posts/{post.PostId}/media", "photo.png", Png, "image/png",
            new Dictionary<string, string> { ["altText"] = " " });
        Assert.Equal(400, sNoAlt);

        var (sUp, media, eUp) = await author.UploadFormAsync<PostMediaDto>(
            $"/api/v1/social/posts/{post.PostId}/media", "photo.png", Png, "image/png",
            new Dictionary<string, string> { ["altText"] = "متطوع بيوزع كراتين" });
        Assert.Null(eUp);
        Assert.Equal(201, sUp);
        Assert.Equal("متطوع بيوزع كراتين", media!.AltText);
        Assert.False(string.IsNullOrWhiteSpace(media.Url));

        // Someone else cannot attach images to the post.
        var (sForeign, _, _) = await reader.UploadFormAsync<PostMediaDto>(
            $"/api/v1/social/posts/{post.PostId}/media", "photo.png", Png, "image/png",
            new Dictionary<string, string> { ["altText"] = "صورة" });
        Assert.Equal(403, sForeign);

        // The feed and the post detail carry the image.
        var (_, feed, _) = await reader.GetAsync<PagedResult<PostDto>>("/api/v1/social/posts");
        var inFeed = feed!.Items.First(p => p.PostId == post.PostId);
        var feedMedia = Assert.Single(inFeed.Media);
        Assert.Equal(media.MediaId, feedMedia.MediaId);
        Assert.Equal("متطوع بيوزع كراتين", feedMedia.AltText);

        var (_, detail, _) = await reader.GetAsync<PostDetailDto>($"/api/v1/social/posts/{post.PostId}");
        Assert.Single(detail!.Post.Media);

        // At most four images per post.
        for (var i = 0; i < 3; i++)
        {
            var (sMore, _, _) = await author.UploadFormAsync<PostMediaDto>(
                $"/api/v1/social/posts/{post.PostId}/media", $"photo{i}.png", Png, "image/png",
                new Dictionary<string, string> { ["altText"] = $"صورة {i}" });
            Assert.Equal(201, sMore);
        }
        var (sFifth, _, _) = await author.UploadFormAsync<PostMediaDto>(
            $"/api/v1/social/posts/{post.PostId}/media", "photo5.png", Png, "image/png",
            new Dictionary<string, string> { ["altText"] = "خامسة" });
        Assert.Equal(400, sFifth);
    }

    [Fact]
    public async Task Full_public_profile_per_account()
    {
        var meAuth = await Register("volunteer", "Profile Owner " + Suffix, Email("po"), Phone());
        var me = Authed(meAuth.AccessToken);
        var other = Authed((await Register("disabled_user", "Profile Viewer " + Suffix, Email("pv"), Phone())).AccessToken);

        var (sUpd, _, _) = await me.PutAsync<object?>("/api/v1/me/profile", new
        {
            displayName = "Profile Owner " + Suffix,
            bio = "متطوع في القاهرة",
            city = "القاهرة",
            visibility = "public"
        });
        Assert.Equal(200, sUpd);

        var (_, full, _) = await other.GetAsync<PublicProfileDto>($"/api/v1/social/users/{meAuth.UserId}");
        Assert.NotNull(full);
        Assert.Equal("volunteer", full!.Role);
        Assert.Equal("متطوع في القاهرة", full.Bio);
        Assert.Equal("القاهرة", full.City);
        Assert.True(full.MemberSince > DateTime.MinValue);
        Assert.NotNull(full.Volunteer);
        Assert.Null(full.Organization);
        Assert.Null(full.NeedType);
        Assert.False(full.IsMe);

        // A private profile keeps bio and city for the owner only.
        await me.PutAsync<object?>("/api/v1/me/profile", new
        {
            displayName = "Profile Owner " + Suffix,
            bio = "متطوع في القاهرة",
            city = "القاهرة",
            visibility = "private"
        });
        var (_, hidden, _) = await other.GetAsync<PublicProfileDto>($"/api/v1/social/users/{meAuth.UserId}");
        Assert.Null(hidden!.Bio);
        Assert.Null(hidden.City);
        Assert.Null(hidden.Volunteer);
        Assert.Equal("Profile Owner " + Suffix, hidden.DisplayName);

        var (_, own, _) = await me.GetAsync<PublicProfileDto>($"/api/v1/social/users/{meAuth.UserId}");
        Assert.True(own!.IsMe);
        Assert.Equal("متطوع في القاهرة", own.Bio);
    }
}
