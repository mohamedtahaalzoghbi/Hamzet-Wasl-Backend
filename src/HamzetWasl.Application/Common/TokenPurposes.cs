namespace HamzetWasl.Application.Common;

/// <summary>
/// Purposes carried by the tokens the API issues. Only <see cref="Access"/> tokens may
/// authenticate a protected endpoint; refresh and registration tickets are bound to their
/// own flow and are rejected by the bearer pipeline.
/// </summary>
public static class TokenPurposes
{
    /// <summary>Name of the claim that carries the token purpose.</summary>
    public const string Claim = "purpose";

    /// <summary>Access token: short lived, sent as <c>Authorization: Bearer</c>.</summary>
    public const string Access = "access";

    /// <summary>Refresh token: single use, exchanged for a new token pair.</summary>
    public const string Refresh = "refresh";

    /// <summary>Short lived ticket that lets a first-time Google user finish registration.</summary>
    public const string GoogleRegistration = "google_registration";
}
