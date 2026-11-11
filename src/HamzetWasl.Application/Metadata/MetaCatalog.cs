using HamzetWasl.Application.Contracts.Meta;

namespace HamzetWasl.Application.Metadata;

/// <summary>
/// Static reference data for values that have no dedicated table in the SQL
/// schema (business fields, employee ranges, and starter skill/interest/
/// accommodation lists). The governorates/areas here are the <b>fallback</b>
/// catalogue: the live data is read from GOVERNORATE/AREA by IGeoCatalog.
/// </summary>
public static class MetaCatalog
{
    public static readonly IReadOnlyList<AccountTypeDto> AccountTypes =
    [
        new("company", "شركة", "تقدم فرص وخدمات"),
        new("disabled_user", "ذوي همم", "ابحث عن وظائف وساعد الآخرين"),
        new("association", "جمعية / منظمة", "شارك بخدمات ومبادرات"),
        new("volunteer", "متطوع", "شارك بمهاراتك وساعد الآخرين")
    ];

    public static readonly IReadOnlyList<BusinessFieldDto> BusinessFields =
    [
        new("technology", "تكنولوجيا"),
        new("education", "تعليم"),
        new("health", "صحة"),
        new("marketing", "تسويق"),
        new("finance", "مالية"),
        new("engineering", "هندسة"),
        new("other", "أخرى")
    ];

    public static readonly IReadOnlyList<EmployeeRangeDto> EmployeeRanges =
    [
        new("1-10", "1–10"),
        new("11-50", "11–50"),
        new("51-200", "51–200"),
        new("200+", "أكثر من 200")
    ];

    public static readonly IReadOnlyList<GovernorateDto> Governorates =
    [
        new("cairo", "القاهرة", [new("nasr_city", "مدينة نصر"), new("maadi", "المعادي"), new("heliopolis", "مصر الجديدة"), new("downtown", "وسط البلد"), new("zamalek", "الزمالك")]),
        new("giza", "الجيزة", [new("dokki", "الدقي"), new("sheikh_zayed", "الشيخ زايد"), new("haram", "الهرم"), new("october_6", "6 أكتوبر")]),
        new("alexandria", "الإسكندرية", [new("smouha", "سموحة"), new("sidi_gaber", "سيدي جابر"), new("montazah", "المنتزه")]),
        new("dakahlia", "الدقهلية", [new("mansoura", "المنصورة"), new("mit_ghamr", "ميت غمر")]),
        new("sharqia", "الشرقية", [new("zagazig", "الزقازيق"), new("10th_of_ramadan", "العاشر من رمضان")]),
        new("gharbia", "الغربية", [new("tanta", "طنطا"), new("mahalla", "المحلة الكبرى")]),
        new("monufia", "المنوفية", [new("shebin_el_kom", "شبين الكوم")]),
        new("qalyubia", "القليوبية", [new("banha", "بنها"), new("shubra_el_kheima", "شبرا الخيمة")]),
        new("kafr_el_sheikh", "كفر الشيخ", [new("kafr_el_sheikh_city", "كفر الشيخ")]),
        new("damietta", "دمياط", [new("damietta_city", "دمياط")]),
        new("port_said", "بورسعيد", [new("port_said_city", "بورسعيد")]),
        new("ismailia", "الإسماعيلية", [new("ismailia_city", "الإسماعيلية")]),
        new("suez", "السويس", [new("suez_city", "السويس")]),
        new("north_sinai", "شمال سيناء", [new("arish", "العريش")]),
        new("south_sinai", "جنوب سيناء", [new("sharm_el_sheikh", "شرم الشيخ")]),
        new("red_sea", "البحر الأحمر", [new("hurghada", "الغردقة")]),
        new("beheira", "البحيرة", [new("damanhur", "دمنهور")]),
        new("matrouh", "مطروح", [new("marsa_matrouh", "مرسى مطروح")]),
        new("fayoum", "الفيوم", [new("fayoum_city", "الفيوم")]),
        new("beni_suef", "بني سويف", [new("beni_suef_city", "بني سويف")]),
        new("minya", "المنيا", [new("minya_city", "المنيا")]),
        new("assiut", "أسيوط", [new("assiut_city", "أسيوط"), new("abnoub", "أبنوب")]),
        new("sohag", "سوهاج", [new("sohag_city", "سوهاج")]),
        new("qena", "قنا", [new("qena_city", "قنا")]),
        new("luxor", "الأقصر", [new("luxor_city", "الأقصر")]),
        new("aswan", "أسوان", [new("aswan_city", "أسوان")]),
        new("new_valley", "الوادي الجديد", [new("kharga", "الخارجة")])
    ];

    public static readonly IReadOnlyList<(string Key, string NameAr)> StarterSkills =
    [
        ("teaching", "تدريس"), ("translation", "ترجمة"), ("programming", "برمجة"),
        ("design", "تصميم"), ("first_aid", "إسعافات أولية"), ("psychological_support", "دعم نفسي"),
        ("accompaniment", "مرافقة"), ("audio_reading", "قراءة صوتية"), ("sign_language", "لغة إشارة"),
        ("it_support", "حل مشكلات تقنية"), ("marketing", "تسويق"), ("accounting", "محاسبة")
    ];

    public static readonly IReadOnlyList<(string Key, string NameAr)> StarterInterests =
    [
        ("education", "تعليم"), ("health", "صحة"), ("technology", "تكنولوجيا"),
        ("arts", "فنون"), ("sports", "رياضة"), ("community", "مجتمع"),
        ("jobs", "وظائف"), ("accessibility", "إتاحة"), ("travel", "سفر"), ("culture", "ثقافة")
    ];

    public static readonly IReadOnlyList<(string Key, string NameAr, string Category)> StarterAccommodations =
    [
        ("ramp", "منحدر", "physical"), ("elevator", "مصعد", "physical"),
        ("accessible_restroom", "دورة مياه مجهزة", "physical"), ("visual_alarm", "إنذار بصري", "communication"),
        ("sign_language", "لغة إشارة", "communication"), ("flexible_hours", "ساعات مرنة", "workplace"),
        ("remote_work", "عمل عن بُعد", "workplace"), ("assistive_technology", "تقنيات مساعدة", "workplace"),
        ("reader", "قارئ", "workplace"), ("interpreter", "مترجم", "workplace")
    ];

    public static readonly IReadOnlyList<(string Key, string NameAr, string DescriptionAr)> NeedTypeCatalog =
    [
        ("visual", "بصرية", "إعدادات وتفضيلات إتاحة بصرية"),
        ("motor", "حركية", "إعدادات وتفضيلات إتاحة حركية"),
        ("hearing", "سمعية", "إعدادات وتفضيلات إتاحة سمعية")
    ];

    public static GovernorateDto? FindGovernorate(string? keyOrName) =>
        Governorates.FirstOrDefault(g =>
            string.Equals(g.Key, keyOrName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(g.NameAr, keyOrName, StringComparison.Ordinal));

    public static AreaDto? FindArea(GovernorateDto governorate, string? keyOrName) =>
        governorate.Areas.FirstOrDefault(a =>
            string.Equals(a.Key, keyOrName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a.NameAr, keyOrName, StringComparison.Ordinal));

    public static bool IsValidField(string? key) =>
        key is not null && BusinessFields.Any(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));

    public static bool IsValidEmployeeRange(string? key) =>
        key is not null && EmployeeRanges.Any(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));
}
