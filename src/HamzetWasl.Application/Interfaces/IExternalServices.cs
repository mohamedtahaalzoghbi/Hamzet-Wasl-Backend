namespace HamzetWasl.Application.Interfaces;

public interface IClock
{
    DateTime UtcNow { get; }
}

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}

public interface ISmsSender
{
    Task SendAsync(string phone, string message, CancellationToken ct = default);
}

public sealed record GoogleUserInfo(string Subject, string Email, string? Name, bool EmailVerified);

public interface IGoogleTokenValidator
{
    Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken ct = default);
}
