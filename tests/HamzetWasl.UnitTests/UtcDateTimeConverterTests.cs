using System.Text.Json;
using HamzetWasl.Api.Serialization;
using Xunit;

namespace HamzetWasl.UnitTests;

/// <summary>
/// Every timestamp the API returns must be an explicit UTC instant. SQL Server hands the
/// provider <c>DATETIME2</c> values with <see cref="DateTimeKind.Unspecified"/>, so those
/// must be treated as UTC and never as server-local time.
/// </summary>
public class UtcDateTimeConverterTests
{
    private sealed record Sample(DateTime Timestamp);

    private static string Write(DateTime value)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        JsonDefaults.Configure(options);
        return JsonSerializer.Serialize(new Sample(value), options);
    }

    [Fact]
    public void An_unspecified_timestamp_is_written_as_utc()
    {
        var value = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Unspecified);

        var json = Write(value);

        Assert.Contains("2026-01-15T10:30:00Z", json, StringComparison.Ordinal);
    }

    [Fact]
    public void A_local_timestamp_is_converted_to_utc()
    {
        var utc = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var local = utc.ToLocalTime();

        var json = Write(local);

        Assert.Contains("2026-01-15T10:30:00Z", json, StringComparison.Ordinal);
    }

    [Fact]
    public void A_utc_timestamp_keeps_its_instant()
    {
        var value = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        var json = Write(value);

        Assert.Contains("2026-01-15T10:30:00Z", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Reading_a_timestamp_without_an_offset_assumes_utc()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        JsonDefaults.Configure(options);

        var read = JsonSerializer.Deserialize<Sample>("""{"timestamp":"2026-01-15T10:30:00"}""", options);

        Assert.NotNull(read);
        Assert.Equal(DateTimeKind.Utc, read!.Timestamp.Kind);
        Assert.Equal(new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc), read.Timestamp);
    }

    [Fact]
    public void Reading_a_timestamp_with_an_offset_converts_it_to_utc()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        JsonDefaults.Configure(options);

        var read = JsonSerializer.Deserialize<Sample>("""{"timestamp":"2026-01-15T12:30:00+02:00"}""", options);

        Assert.NotNull(read);
        Assert.Equal(DateTimeKind.Utc, read!.Timestamp.Kind);
        Assert.Equal(new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc), read.Timestamp);
    }

    [Fact]
    public void The_shared_options_are_the_same_for_mvc_and_the_manual_writers()
    {
        // JsonDefaults is used by the exception middleware and the auth challenge; MVC must
        // produce byte-identical payloads, otherwise the same field would look different
        // depending on which code path answered the request.
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        JsonDefaults.Configure(options);

        var viaMvc = JsonSerializer.Serialize(new Sample(new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Unspecified)), options);
        var viaDefaults = JsonSerializer.Serialize(new Sample(new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Unspecified)), JsonDefaults.Options);

        Assert.Equal(viaMvc, viaDefaults);
    }
}
