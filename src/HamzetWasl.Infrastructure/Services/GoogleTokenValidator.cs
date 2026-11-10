using Google.Apis.Auth;
using HamzetWasl.Application.Common;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace HamzetWasl.Infrastructure.Services;

public sealed class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly GoogleOptions _options;

    public GoogleTokenValidator(IOptions<GoogleOptions> options) => _options = options.Value;

    public async Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            throw new AppException(503, ErrorCodes.GoogleNotConfigured);
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [_options.ClientId]
            });
            if (string.IsNullOrWhiteSpace(payload.Email)) return null;
            return new GoogleUserInfo(payload.Subject, payload.Email, payload.Name, payload.EmailVerified);
        }
        catch (InvalidJwtException)
        {
            throw new AppException(401, ErrorCodes.GoogleTokenInvalid);
        }
    }
}
