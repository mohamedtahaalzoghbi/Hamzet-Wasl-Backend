using System.Globalization;
using System.Text.Json;
using HamzetWasl.Domain.Constants;

namespace HamzetWasl.Domain.Rules;

/// <summary>The accessibility settings a client should actually render.</summary>
public sealed record AccessibilityPresetValues(
    string Theme,
    decimal FontScale,
    bool ReducedMotion,
    string InteractionMode,
    string NotificationMode,
    bool HighContrast,
    string TextDirection);

/// <summary>The calculated preset together with the need types that produced it.</summary>
public sealed record AccessibilityPresetResult(
    AccessibilityPresetValues Values,
    IReadOnlyList<string> NeedTypes);

/// <summary>
/// Calculates the effective accessibility preset.
/// <para>
/// The precedence is: <b>manual overrides â†’ the user's own preferences â†’ need-type defaults â†’ neutral defaults</b>.
/// </para>
/// <list type="bullet">
/// <item>A value the user changed away from the neutral baseline is always kept.</item>
/// <item>A value the user never touched (still neutral) is filled from the declared need types.</item>
/// <item>Several need types can be combined: the declared one first, then any extra need types
/// listed in <c>manual_overrides.needTypes</c>; the first need type that suggests a value wins.</item>
/// <item><c>manual_overrides</c> has the highest priority and may set any of the preset keys.</item>
/// </list>
/// </summary>
public static class AccessibilityPresets
{
    /// <summary>Neutral baseline values: they mean "the user did not choose".</summary>
    public const string NeutralTheme = AccessibilityThemes.System;

    public const decimal NeutralFontScale = 1.0m;

    public const bool NeutralReducedMotion = false;

    public const string NeutralInteractionMode = InteractionModes.Touch;

    public const string NeutralNotificationMode = NotificationModes.InApp;

    /// <summary>Key used inside <c>manual_overrides</c> to declare additional need types.</summary>
    public const string OverridesNeedTypesKey = "needTypes";

    /// <summary>Calculates the effective preset for a stored accessibility profile.</summary>
    public static AccessibilityPresetResult Calculate(
        string? declaredNeedType,
        string storedTheme,
        decimal storedFontScale,
        bool storedReducedMotion,
        string storedInteractionMode,
        string storedNotificationMode,
        string? manualOverridesJson)
    {
        var overrides = ParseOverrides(manualOverridesJson);
        var needTypes = CollectNeedTypes(declaredNeedType, overrides);

        var theme = NeutralTheme;
        var fontScale = NeutralFontScale;
        var reducedMotion = NeutralReducedMotion;
        var interactionMode = NeutralInteractionMode;
        var notificationMode = NeutralNotificationMode;

        // 1) need-type defaults (first need type that suggests a value wins).
        foreach (var needType in needTypes)
        {
            var defaults = DefaultsFor(needType);
            if (defaults is null)
            {
                continue;
            }

            theme = KeepFirstTheme(theme, defaults.Theme);
            fontScale = KeepFirstScale(fontScale, defaults.FontScale);
            reducedMotion = KeepFirstMotion(reducedMotion, defaults.ReducedMotion);
            interactionMode = KeepFirstInteraction(interactionMode, defaults.InteractionMode);
            notificationMode = KeepFirstNotification(notificationMode, defaults.NotificationMode);
        }

        // 2) the user's own preferences win over the need-type defaults.
        if (!IsNeutralTheme(storedTheme))
        {
            theme = storedTheme;
        }

        if (storedFontScale != NeutralFontScale)
        {
            fontScale = storedFontScale;
        }

        if (storedReducedMotion != NeutralReducedMotion)
        {
            reducedMotion = storedReducedMotion;
        }

        if (!IsNeutralInteractionMode(storedInteractionMode))
        {
            interactionMode = storedInteractionMode;
        }

        if (!IsNeutralNotificationMode(storedNotificationMode))
        {
            notificationMode = storedNotificationMode;
        }

        // 3) manual overrides have the highest priority.
        var highContrast = theme == AccessibilityThemes.HighContrast;
        var textDirection = "rtl";

        if (overrides.TryGetValue("theme", out var overrideTheme))
        {
            theme = overrideTheme.ToString()!;
        }

        if (overrides.TryGetValue("fontScale", out var overrideScale))
        {
            if (overrideScale.ValueKind == JsonValueKind.Number && overrideScale.TryGetDecimal(out var scale))
            {
                fontScale = scale;
            }
        }

        if (overrides.TryGetValue("reducedMotion", out var overrideMotion))
        {
            if (overrideMotion.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                reducedMotion = overrideMotion.GetBoolean();
            }
        }

        if (overrides.TryGetValue("interactionMode", out var overrideInteraction))
        {
            interactionMode = overrideInteraction.ToString()!;
        }

        if (overrides.TryGetValue("notificationMode", out var overrideNotification))
        {
            notificationMode = overrideNotification.ToString()!;
        }

        if (overrides.TryGetValue("highContrast", out var overrideContrast))
        {
            if (overrideContrast.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                highContrast = overrideContrast.GetBoolean();
            }
        }

        if (overrides.TryGetValue("textDirection", out var overrideDirection))
        {
            textDirection = overrideDirection.ToString()!;
        }

        var values = new AccessibilityPresetValues(
            theme,
            fontScale,
            reducedMotion,
            interactionMode,
            notificationMode,
            highContrast,
            textDirection);

        return new AccessibilityPresetResult(values, needTypes);
    }

    /// <summary>The defaults a need type suggests when the user has not chosen a value.</summary>
    public static AccessibilityPresetValues? DefaultsFor(string needType) => needType switch
    {
        NeedTypes.Visual => new AccessibilityPresetValues(
            AccessibilityThemes.HighContrast,
            1.25m,
            false,
            InteractionModes.ScreenReader,
            NotificationModes.VisualAlert,
            true,
            "rtl"),
        NeedTypes.Hearing => new AccessibilityPresetValues(
            AccessibilityThemes.Dark,
            NeutralFontScale,
            false,
            NeutralInteractionMode,
            NotificationModes.VisualAlert,
            false,
            "rtl"),
        NeedTypes.Motor => new AccessibilityPresetValues(
            AccessibilityThemes.Light,
            NeutralFontScale,
            true,
            InteractionModes.Keyboard,
            NotificationModes.Vibration,
            false,
            "rtl"),
        _ => null
    };

    private static string KeepFirstTheme(string current, string candidate) =>
        IsNeutralTheme(current) ? candidate : current;

    private static decimal KeepFirstScale(decimal current, decimal candidate) =>
        current == NeutralFontScale ? candidate : current;

    private static bool KeepFirstMotion(bool current, bool candidate) =>
        current == NeutralReducedMotion ? candidate : current;

    private static string KeepFirstInteraction(string current, string candidate) =>
        IsNeutralInteractionMode(current) ? candidate : current;

    private static string KeepFirstNotification(string current, string candidate) =>
        IsNeutralNotificationMode(current) ? candidate : current;

    private static bool IsNeutralTheme(string value) =>
        string.IsNullOrWhiteSpace(value) ||
        string.Equals(value, NeutralTheme, StringComparison.OrdinalIgnoreCase);

    private static bool IsNeutralInteractionMode(string value) =>
        string.IsNullOrWhiteSpace(value) ||
        string.Equals(value, NeutralInteractionMode, StringComparison.OrdinalIgnoreCase);

    private static bool IsNeutralNotificationMode(string value) =>
        string.IsNullOrWhiteSpace(value) ||
        string.Equals(value, NeutralNotificationMode, StringComparison.OrdinalIgnoreCase);

    /// <summary>The declared need type plus any extra need types declared in the manual overrides.</summary>
    private static IReadOnlyList<string> CollectNeedTypes(string? declaredNeedType, IReadOnlyDictionary<string, JsonElement> overrides)
    {
        var result = new List<string>();

        if (!string.IsNullOrWhiteSpace(declaredNeedType) && NeedTypes.All.Contains(declaredNeedType))
        {
            result.Add(declaredNeedType);
        }

        if (overrides.TryGetValue(OverridesNeedTypesKey, out var extra) && extra.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in extra.EnumerateArray())
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value) &&
                    NeedTypes.All.Contains(value) &&
                    !result.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(value);
                }
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<string, JsonElement> ParseOverrides(string? manualOverridesJson)
    {
        if (string.IsNullOrWhiteSpace(manualOverridesJson))
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var document = JsonDocument.Parse(manualOverridesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            }

            return document.RootElement.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
