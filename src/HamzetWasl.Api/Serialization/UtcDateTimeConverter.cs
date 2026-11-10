using System.Text.Json;
using System.Text.Json.Serialization;

namespace HamzetWasl.Api.Serialization;

/// <summary>
/// Writes every timestamp as an explicit UTC instant (<c>2026-01-15T10:30:00Z</c>) and reads
/// incoming timestamps as UTC.
/// <para>
/// SQL Server stores the schema timestamps in <c>DATETIME2</c> and always in UTC, but the
/// provider materialises them as <see cref="DateTimeKind.Unspecified"/>. Treating those as
/// local time would shift every timestamp by the server offset, so an unspecified value is
/// interpreted as UTC.
/// </para>
/// </summary>
public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();
        return ToUtc(value);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(ToUtc(value));

    /// <summary>Normalises a timestamp to UTC: local values are converted, unspecified values are assumed to be UTC.</summary>
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
