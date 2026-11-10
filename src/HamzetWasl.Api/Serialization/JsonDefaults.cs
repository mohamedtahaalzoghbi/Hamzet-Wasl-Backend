using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace HamzetWasl.Api.Serialization;

/// <summary>
/// Shared JSON settings for MVC and the few places that serialize manually
/// (auth challenge/forbid, rate limiting, and the exception middleware).
/// The same configuration is applied to MVC through <see cref="Configure"/> so both
/// pipelines always produce identical payloads.
/// </summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = Create();

    /// <summary>Applies the shared settings to an existing serializer options instance.</summary>
    public static void Configure(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        options.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);

        options.Converters.Remove(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));

        // Timestamps are always emitted as UTC instants.
        options.Converters.Remove(new UtcDateTimeConverter());
        options.Converters.Add(new UtcDateTimeConverter());
    }

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options);
        return options;
    }
}
