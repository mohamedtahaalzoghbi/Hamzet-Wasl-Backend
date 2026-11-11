using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HamzetWasl.Domain.Constants;

namespace HamzetWasl.Domain.Rules;

/// <summary>
/// يقرأ ويكتب قائمة الفئات المتعددة داخل عمود <c>manual_overrides</c> بصيغة:
/// <code>{ "needTypes": ["visual", "hearing"], ...بقية الإعدادات... }</code>
///
/// الجدول يحتفظ بعمود واحد <c>need_type_id</c> للفئة الأساسية، لذلك تُخزَّن الفئات الإضافية
/// في هذا المفتاح JSON بدلاً من تغيير المخطط. العنصر الأول في المصفوفة هو الفئة الأساسية
/// دائماً (نفس <c>need_type_id</c>)، وبقية العناصر هي الفئات الإضافية.
/// </summary>
public static class AccessibilityOverrides
{
    /// <summary>اسم المفتاح داخل JSON.</summary>
    public const string NeedTypesKey = AccessibilityPresets.OverridesNeedTypesKey;

    /// <summary>الحد الأقصى لحجم JSON المخصص (4 كيلوبايت بترميز UTF-8).</summary>
    public const int MaxJsonBytes = 4 * 1024;

    public const int MinNeedTypes = 1;

    public const int MaxNeedTypes = 3;

    /// <summary>حد حجم الإعدادات المخصصة؛ القيمة الفارغة مقبولة.</summary>
    public static bool IsWithinSizeLimit(string? json) =>
        string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) <= MaxJsonBytes;

    /// <summary>
    /// يدمج الفئات المطلوبة في كائن <c>manual_overrides</c> مع الحفاظ على بقية المفاتيح
    /// (theme, fontScale, ...). عند تمرير <paramref name="needTypes"/> بقيمة <c>null</c>
    /// يُترك الـ JSON كما هو (توافق مع العملاء القدامى الذين كتبوا needTypes يدوياً).
    /// </summary>
    public static string? Merge(
        string? manualOverridesJson,
        IReadOnlyList<string>? needTypes,
        string? primaryNeedType)
    {
        // عميل قديم لا يرسل الحقل الجديد: لا نلمس JSON الذي أرسله.
        if (needTypes is null)
        {
            return manualOverridesJson;
        }

        var root = ParseObject(manualOverridesJson);
        var normalized = NormalizeNeedTypes(needTypes, primaryNeedType);

        if (normalized.Count > 1)
        {
            var array = new JsonArray();
            foreach (var needType in normalized)
            {
                array.Add(needType);
            }

            root[NeedTypesKey] = array;
        }
        else
        {
            // فئة واحدة: الفئة الأساسية موجودة أصلاً في need_type_id، فلا داعي لتكرارها.
            // الحذف مهم أيضاً عند التحويل من فئتين إلى فئة واحدة حتى لا تبقى الفئة القديمة.
            root.Remove(NeedTypesKey);
        }

        return root.Count == 0 ? null : root.ToJsonString();
    }

    /// <summary>
    /// يُرجع الفئات بالترتيب: الفئة الأساسية أولاً ثم الفئات الإضافية المعروفة، بلا تكرار.
    /// يتجاهل القيم غير المعروفة بدلاً من تمريرها إلى قاعدة البيانات.
    /// </summary>
    public static IReadOnlyList<string> NormalizeNeedTypes(
        IReadOnlyList<string>? needTypes,
        string? primaryNeedType)
    {
        var result = new List<string>();
        var primary = primaryNeedType?.Trim();

        if (!string.IsNullOrWhiteSpace(primary) && NeedTypes.All.Contains(primary))
        {
            result.Add(primary);
        }

        if (needTypes is not null)
        {
            foreach (var item in needTypes)
            {
                var value = item?.Trim();
                if (!string.IsNullOrWhiteSpace(value) &&
                    NeedTypes.All.Contains(value) &&
                    !result.Contains(value, StringComparer.Ordinal))
                {
                    result.Add(value);
                }
            }
        }

        // حماية إضافية: لو كان الطلب غير صالح تماماً نضمن وجود الفئة الأساسية على الأقل.
        if (result.Count == 0 && !string.IsNullOrWhiteSpace(primary))
        {
            result.Add(primary);
        }

        return result;
    }

    private static JsonObject ParseObject(string? manualOverridesJson)
    {
        if (string.IsNullOrWhiteSpace(manualOverridesJson))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(manualOverridesJson) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            // الـ validator يرفض JSON غير الصالح قبل الوصول إلى هنا؛ هذا احتياط فقط.
            return new JsonObject();
        }
    }
}
