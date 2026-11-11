using System.Text.Json;

namespace HamzetWasl.Application.Contracts.Profile;

public sealed class UpdateProfileRequest
{
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? City { get; set; }
    public string Visibility { get; set; } = "public";
}

public sealed record ProfileResponse(
    Guid UserId,
    string DisplayName,
    string? Bio,
    string? City,
    string? AvatarUrl,
    string Visibility);

/// <summary>Answer of the avatar upload: the public URL of the stored image.</summary>
public sealed record AvatarResponse(string AvatarUrl);

public sealed class SetInterestsRequest
{
    public IReadOnlyList<int> InterestIds { get; set; } = Array.Empty<int>();
}

public sealed class SetSkillsRequest
{
    public IReadOnlyList<int> SkillIds { get; set; } = Array.Empty<int>();
}

public sealed class UpsertAccessibilityRequest
{
    public string NeedType { get; set; } = string.Empty;
    public string Theme { get; set; } = "system";
    public decimal FontScale { get; set; } = 1.0m;
    public bool ReducedMotion { get; set; }
    public string InteractionMode { get; set; } = "touch";
    public string NotificationMode { get; set; } = "in_app";
    public JsonElement? ManualOverrides { get; set; }
}

public sealed record AccessibilityPresetDto(
    string Theme,
    decimal FontScale,
    bool ReducedMotion,
    string InteractionMode,
    string NotificationMode,
    bool HighContrast,
    string TextDirection,
    string? NeedType,
    IReadOnlyList<string> NeedTypes);

public sealed record AccessibilityResponse(
    Guid UserId,
    string NeedType,
    string Theme,
    decimal FontScale,
    bool ReducedMotion,
    string InteractionMode,
    string NotificationMode,
    JsonElement? ManualOverrides,
    DateTime UpdatedAt,
    AccessibilityPresetDto EffectivePreset);

/// <summary>
/// The account-deletion request that was just created. It stays <c>pending</c> until an
/// admin resolves it; the account remains usable in the meantime.
/// </summary>
public sealed record DeletionRequestResponse(
    Guid DeletionRequestId,
    string Status,
    DateTime RequestedAt);
