using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Auth;
using Xunit;

namespace HamzetWasl.IntegrationTests;

/// <summary>Small HTTP helpers that unwrap the standard response envelope.</summary>
public static class TestHttp
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static async Task<(int Status, T Data, ApiError? Error)> SendAsync<T>(
        this HttpClient client, HttpMethod method, string path, object? body = default)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        var response = await client.SendAsync(request);
        return await ReadEnvelopeAsync<T>(response);
    }

    public static Task<(int Status, T Data, ApiError? Error)> GetAsync<T>(
        this HttpClient client, string path) =>
        client.SendAsync<T>(HttpMethod.Get, path);

    public static Task<(int Status, T Data, ApiError? Error)> DeleteAsync<T>(
        this HttpClient client, string path) =>
        client.SendAsync<T>(HttpMethod.Delete, path);

    public static Task<(int Status, T Data, ApiError? Error)> PostAsync<T>(
        this HttpClient client, string path, object? body = default) =>
        client.SendAsync<T>(HttpMethod.Post, path, body);

    public static Task<(int Status, T Data, ApiError? Error)> PutAsync<T>(
        this HttpClient client, string path, object? body = default) =>
        client.SendAsync<T>(HttpMethod.Put, path, body);

    public static async Task<(int Status, T Data, ApiError? Error)> UploadAsync<T>(
        this HttpClient client, string path, string fileName, byte[] bytes, string contentType,
        string fieldName = "file")
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new(contentType);
        content.Add(fileContent, fieldName, fileName);

        var response = await client.PutAsync(path, content);
        return await ReadEnvelopeAsync<T>(response);
    }

    /// <summary>POSTs a multipart form with one file plus extra string fields.</summary>
    public static async Task<(int Status, T Data, ApiError? Error)> UploadFormAsync<T>(
        this HttpClient client, string path, string fileName, byte[] bytes, string contentType,
        IDictionary<string, string> fields, string fileField = "file")
    {
        using var content = new MultipartFormDataContent();
        foreach (var field in fields)
        {
            content.Add(new StringContent(field.Value), field.Key);
        }

        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new(contentType);
        content.Add(fileContent, fileField, fileName);

        var response = await client.PostAsync(path, content);
        return await ReadEnvelopeAsync<T>(response);
    }

    /// <summary>
    /// Runs the staged registration flow end to end: POST /auth/register sends the phone
    /// OTP (captured by <paramref name="messages"/>), POST /auth/register/complete creates
    /// the account and returns the session. No account exists between the two calls.
    /// </summary>
    public static async Task<AuthResponse> RegisterVerifiedAsync(
        this HttpClient client, CapturingMessageSender messages, object registerBody)
    {
        var (startStatus, pending, startError) =
            await client.PostAsync<RegisterPendingResponse>("/api/v1/auth/register", registerBody);
        Assert.Equal(200, startStatus);
        Assert.Null(startError);
        Assert.NotNull(pending);

        var code = messages.LastCode;
        Assert.NotNull(code);

        var (status, data, error) = await client.PostAsync<AuthResponse>("/api/v1/auth/register/complete", new
        {
            registrationTicket = pending!.RegistrationTicket,
            code
        });
        Assert.Equal(201, status);
        Assert.Null(error);
        Assert.NotNull(data);
        return data!;
    }

    /// <summary>Reads the envelope but drains the response body even for attempts that are not JSON.</summary>
    private static async Task<(int Status, T Data, ApiError? Error)> ReadEnvelopeAsync<T>(
        HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        if (status == 204)
        {
            return (status, default!, default);
        }

        var body = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            return (status, default!, default);
        }

        var envelope = JsonSerializer.Deserialize<ApiResponse<T>>(body, JsonOptions);
        return (status, envelope!.Data ?? default!, envelope.Error);
    }
}
