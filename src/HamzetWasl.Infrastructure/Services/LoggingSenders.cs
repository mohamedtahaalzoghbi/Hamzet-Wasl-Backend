using HamzetWasl.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace HamzetWasl.Infrastructure.Services;

/// <summary>Development email transport. Replace with SES/SendGrid in production.</summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        _logger.LogInformation("Email dispatched. Subject={Subject} BodyLength={BodyLength} RecipientDomain={Domain}",
            subject, htmlBody.Length, MaskEmail(to));
        return Task.CompletedTask;
    }

    private static string MaskEmail(string value)
    {
        var at = value.IndexOf('@', StringComparison.Ordinal);
        return at <= 0 ? "***" : "***" + value[at..];
    }
}

/// <summary>Development SMS transport. Replace with a local Egyptian SMS gateway.</summary>
public sealed class LoggingSmsSender : ISmsSender
{
    private readonly ILogger<LoggingSmsSender> _logger;

    public LoggingSmsSender(ILogger<LoggingSmsSender> logger) => _logger = logger;

    public Task SendAsync(string phone, string message, CancellationToken ct = default)
    {
        _logger.LogInformation("SMS dispatched. MessageLength={MessageLength} RecipientSuffix={Suffix}",
            message.Length, phone.Length > 3 ? phone[^3..] : "***");
        return Task.CompletedTask;
    }
}
