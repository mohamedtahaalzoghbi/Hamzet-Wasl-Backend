using HamzetWasl.Domain.Rules;
using Xunit;

namespace HamzetWasl.UnitTests;

/// <summary>
/// The effective accessibility preset is calculated from the declared need types, the user's
/// own preferences and the manual overrides (highest priority).
/// </summary>
public class AccessibilityPresetTests
{
    private const string NeutralTheme = AccessibilityPresets.NeutralTheme;
    private const decimal NeutralFontScale = AccessibilityPresets.NeutralFontScale;
    private const bool NeutralReducedMotion = AccessibilityPresets.NeutralReducedMotion;
    private const string NeutralInteractionMode = AccessibilityPresets.NeutralInteractionMode;
    private const string NeutralNotificationMode = AccessibilityPresets.NeutralNotificationMode;

    private static AccessibilityPresetResult Calculate(
        string? needType,
        string? overrides = null,
        string theme = NeutralTheme,
        decimal fontScale = NeutralFontScale,
        bool reducedMotion = NeutralReducedMotion,
        string interactionMode = NeutralInteractionMode,
        string notificationMode = NeutralNotificationMode) =>
        AccessibilityPresets.Calculate(
            needType, theme, fontScale, reducedMotion, interactionMode, notificationMode, overrides);

    [Fact]
    public void Visual_needs_produce_a_high_contrast_large_text_screen_reader_preset()
    {
        var result = Calculate("visual");

        Assert.Equal("visual", Assert.Single(result.NeedTypes));
        Assert.Equal("high_contrast", result.Values.Theme);
        Assert.Equal(1.25m, result.Values.FontScale);
        Assert.True(result.Values.HighContrast);
        Assert.Equal("screen_reader", result.Values.InteractionMode);
        Assert.Equal("visual_alert", result.Values.NotificationMode);
        Assert.Equal("rtl", result.Values.TextDirection);
    }

    [Fact]
    public void Hearing_needs_produce_a_dark_theme_with_visual_alerts()
    {
        var result = Calculate("hearing");

        Assert.Equal("hearing", Assert.Single(result.NeedTypes));
        Assert.Equal("dark", result.Values.Theme);
        Assert.Equal("visual_alert", result.Values.NotificationMode);
        Assert.Equal(NeutralFontScale, result.Values.FontScale);
    }

    [Fact]
    public void Motor_needs_produce_reduced_motion_keyboard_and_vibration()
    {
        var result = Calculate("motor");

        Assert.Equal("motor", Assert.Single(result.NeedTypes));
        Assert.True(result.Values.ReducedMotion);
        Assert.Equal("keyboard", result.Values.InteractionMode);
        Assert.Equal("vibration", result.Values.NotificationMode);
        Assert.Equal("light", result.Values.Theme);
    }

    [Fact]
    public void Several_needs_can_be_combined_without_losing_the_first_need()
    {
        var result = Calculate("visual", """{"needTypes":["visual","hearing","motor"]}""");

        Assert.Equal(3, result.NeedTypes.Count);
        Assert.Equal("visual", result.NeedTypes[0]);
        Assert.Contains("hearing", result.NeedTypes);
        Assert.Contains("motor", result.NeedTypes);

        // visual comes first, so its values win over the later needs.
        Assert.Equal("high_contrast", result.Values.Theme);
        Assert.Equal(1.25m, result.Values.FontScale);
        Assert.Equal("screen_reader", result.Values.InteractionMode);
        Assert.Equal("visual_alert", result.Values.NotificationMode);
    }

    [Fact]
    public void Additional_needs_fill_the_values_the_first_need_does_not_define()
    {
        // motor defines reduced motion, visual does not -> motor contributes it.
        var result = Calculate("visual", """{"needTypes":["motor"]}""");

        Assert.True(result.Values.ReducedMotion);
        Assert.Equal("high_contrast", result.Values.Theme);
    }

    [Fact]
    public void Manual_overrides_win_over_need_defaults()
    {
        var result = Calculate("visual", """{"theme":"light","fontScale":2,"reducedMotion":true}""");

        Assert.Equal("light", result.Values.Theme);
        Assert.Equal(2m, result.Values.FontScale);
        Assert.True(result.Values.ReducedMotion);
        // not overridden -> still the need default
        Assert.Equal("screen_reader", result.Values.InteractionMode);
    }

    [Fact]
    public void Manual_overrides_can_force_the_high_contrast_and_direction_flags()
    {
        var result = Calculate("hearing", """{"highContrast":true,"textDirection":"ltr"}""");

        Assert.True(result.Values.HighContrast);
        Assert.Equal("ltr", result.Values.TextDirection);
    }

    [Fact]
    public void Empty_needs_fall_back_to_the_neutral_defaults()
    {
        var result = Calculate(null);

        Assert.Empty(result.NeedTypes);
        Assert.Equal(NeutralTheme, result.Values.Theme);
        Assert.Equal(NeutralFontScale, result.Values.FontScale);
        Assert.False(result.Values.ReducedMotion);
        Assert.Equal(NeutralInteractionMode, result.Values.InteractionMode);
        Assert.Equal(NeutralNotificationMode, result.Values.NotificationMode);
        Assert.False(result.Values.HighContrast);
    }

    [Fact]
    public void An_unknown_need_type_is_ignored()
    {
        var result = Calculate("telepathy");

        Assert.Empty(result.NeedTypes);
        Assert.Equal(NeutralTheme, result.Values.Theme);
    }

    [Fact]
    public void The_users_own_preferences_win_over_the_need_defaults()
    {
        var result = Calculate(
            "visual",
            theme: "dark",
            fontScale: 1.5m,
            reducedMotion: true,
            interactionMode: "voice",
            notificationMode: "push");

        Assert.Equal("dark", result.Values.Theme);
        Assert.Equal(1.5m, result.Values.FontScale);
        Assert.True(result.Values.ReducedMotion);
        Assert.Equal("voice", result.Values.InteractionMode);
        Assert.Equal("push", result.Values.NotificationMode);
    }

    [Fact]
    public void Conflicting_settings_resolve_by_priority_manual_over_user_over_need()
    {
        // need default (high_contrast) < user choice (dark) < manual override (light)
        var result = Calculate("visual", """{"theme":"light"}""", theme: "dark");

        Assert.Equal("light", result.Values.Theme);
    }

    [Fact]
    public void Malformed_manual_overrides_are_ignored_instead_of_throwing()
    {
        var result = Calculate("visual", "{not json");

        Assert.Equal("high_contrast", result.Values.Theme);
        Assert.Single(result.NeedTypes);
    }

    [Fact]
    public void Unknown_override_keys_are_ignored()
    {
        var result = Calculate("visual", """{"unknownKey":"value"}""");

        Assert.Equal("high_contrast", result.Values.Theme);
    }
}
