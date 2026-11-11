using System.Text;
using System.Text.Json;
using HamzetWasl.Domain.Rules;
using Xunit;

namespace HamzetWasl.UnitTests;

/// <summary>
/// Batch 4: the multiple need types live in <c>manual_overrides.needTypes</c> as JSON so the
/// single <c>need_type_id</c> column of the authoritative schema does not have to change.
/// </summary>
public class AccessibilityOverridesTests
{
    [Fact]
    public void Merge_stores_multiple_types_primary_first_and_keeps_the_other_overrides()
    {
        var merged = AccessibilityOverrides.Merge(
            """{"theme":"light","fontScale":1.5,"highContrast":true}""",
            new[] { "hearing", "visual", "hearing", "unknown" },
            "visual");

        Assert.NotNull(merged);
        using var document = JsonDocument.Parse(merged!);
        var root = document.RootElement;

        Assert.Equal("light", root.GetProperty("theme").GetString());
        Assert.Equal(1.5m, root.GetProperty("fontScale").GetDecimal());
        Assert.True(root.GetProperty("highContrast").GetBoolean());

        var needTypes = root.GetProperty(AccessibilityOverrides.NeedTypesKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        Assert.Equal(new[] { "visual", "hearing" }, needTypes);
    }

    [Fact]
    public void Merge_with_a_single_type_removes_a_stale_array()
    {
        // عند التحويل من فئتين إلى فئة واحدة يجب ألا تبقى الفئة القديمة في JSON.
        var merged = AccessibilityOverrides.Merge(
            """{"theme":"dark","needTypes":["visual","hearing"]}""",
            new[] { "motor" },
            "motor");

        Assert.NotNull(merged);
        using var document = JsonDocument.Parse(merged!);
        Assert.Equal("dark", document.RootElement.GetProperty("theme").GetString());
        Assert.False(document.RootElement.TryGetProperty(AccessibilityOverrides.NeedTypesKey, out _));
    }

    [Fact]
    public void Merge_without_the_new_field_keeps_the_legacy_json_untouched()
    {
        // العميل القديم الذي يكتب needTypes داخل manualOverrides ما زال يعمل كما هو.
        const string legacy = """{"needTypes":["visual","hearing"],"captions":"on"}""";
        Assert.Equal(legacy, AccessibilityOverrides.Merge(legacy, null, "visual"));
        Assert.Null(AccessibilityOverrides.Merge(null, null, "visual"));
    }

    [Fact]
    public void Merge_with_one_type_and_no_other_overrides_returns_null()
    {
        // الفئة الأساسية محفوظة في need_type_id، لذلك لا حاجة لكتابة JSON فارغ.
        Assert.Null(AccessibilityOverrides.Merge(null, new[] { "visual" }, "visual"));
        Assert.Null(AccessibilityOverrides.Merge("{}", new[] { "visual" }, "visual"));
    }

    [Fact]
    public void Normalize_puts_the_primary_first_and_ignores_unknown_or_duplicate_values()
    {
        var normalized = AccessibilityOverrides.NormalizeNeedTypes(
            new[] { " hearing ", "visual", "hearing", "unknown", "" },
            "visual");

        Assert.Equal(new[] { "visual", "hearing" }, normalized.ToArray());
    }

    [Fact]
    public void The_json_size_limit_is_four_kilobytes_of_utf8()
    {
        Assert.True(AccessibilityOverrides.IsWithinSizeLimit(null));
        Assert.True(AccessibilityOverrides.IsWithinSizeLimit("  "));

        var atLimit = "{\"x\":\"" + new string('a', AccessibilityOverrides.MaxJsonBytes - 8) + "\"}";
        Assert.Equal(AccessibilityOverrides.MaxJsonBytes, Encoding.UTF8.GetByteCount(atLimit));
        Assert.True(AccessibilityOverrides.IsWithinSizeLimit(atLimit));

        var tooLarge = atLimit + " ";
        Assert.Equal(AccessibilityOverrides.MaxJsonBytes + 1, Encoding.UTF8.GetByteCount(tooLarge));
        Assert.False(AccessibilityOverrides.IsWithinSizeLimit(tooLarge));

        // الحد بالبايت وليس بعدد الأحرف: حرف عربي واحد يساوي أكثر من بايت بترميز UTF-8.
        var arabic = "{\"x\":\"" + new string('م', AccessibilityOverrides.MaxJsonBytes / 2) + "\"}";
        Assert.False(AccessibilityOverrides.IsWithinSizeLimit(arabic));
    }
}
