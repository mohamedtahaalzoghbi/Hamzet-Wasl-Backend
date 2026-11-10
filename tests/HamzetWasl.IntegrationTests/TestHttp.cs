using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HamzetWasl.Application.Common;

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
