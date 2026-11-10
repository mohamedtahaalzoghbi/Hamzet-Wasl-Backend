using System.Net.Http.Headers;
using HamzetWasl.Application.Contracts.Auth;
using Xunit;

namespace HamzetWasl.IntegrationTests;

/// <summary>
/// The upload limits are part of the contract: an oversized file answers 413
/// FILE_TOO_LARGE, an oversized request answers 413 REQUEST_TOO_LARGE and no framework
/// generated English message ever reaches the client.
/// </summary>
[Collection("Api")]
public class UploadLimitsTests(ApiFixture fixture)
{
    private static byte[] Png(int sizeInBytes)
    {
        var bytes = new byte[sizeInBytes];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes, 0);
        return bytes;
    }

    private async Task<AuthResponse> RegisterAsync(string label)
    {
        var email = $"{label}{Guid.NewGuid().ToString("N")[..8]}@example.com";
        var phone = "010" + (DateTime.UtcNow.Ticks % 100_000_000).ToString("D8");

        var (status, data, error) = await fixture.AnonymousClient.PostAsync<AuthResponse>(
            "/api/v1/auth/register", new
            {
                role = "disabled_user",
                name = "Upload Tester",
                email,
                phone,
                password = "Passw0rd!",
                confirmPassword = "Passw0rd!",
                termsAccepted = true,
                policyVersion = "1.0"
            });

        Assert.Equal(201, status);
        Assert.Null(error);
        Assert.NotNull(data);
        return data!;
    }

    private async Task<(int Status, string Body)> UploadAsync(HttpClient client, byte[] content)
    {
        using var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipart.Add(file, "file", "avatar.png");

        var response = await client.PutAsync("/api/v1/me/profile/avatar", multipart);
        var body = await response.Content.ReadAsStringAsync();
        return ((int)response.StatusCode, body);
    }

    [Fact]
    public async Task A_file_over_the_category_limit_answers_413_file_too_large()
    {
        var user = await RegisterAsync("upload");
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);

        // 5 MB is the limit for a public image; the request limit leaves room for the form.
        var (status, body) = await UploadAsync(client, Png(5 * 1024 * 1024 + 64 * 1024));

        Assert.Equal(413, status);
        Assert.Contains("FILE_TOO_LARGE", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Failed to read", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_request_over_the_limit_answers_413_request_too_large()
    {
        var user = await RegisterAsync("upload2");
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);

        var (status, body) = await UploadAsync(client, Png(8 * 1024 * 1024));

        Assert.True(status == 413 && body.Contains("REQUEST_TOO_LARGE", StringComparison.Ordinal),
            $"status={status} body={body}");
    }

    [Fact]
    public async Task A_file_inside_the_limit_is_accepted()
    {
        var user = await RegisterAsync("upload3");
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);

        var (status, body) = await UploadAsync(client, Png(1024 * 1024));

        Assert.Equal(200, status);
        Assert.Contains("avatarUrl", body, StringComparison.Ordinal);
    }
}
