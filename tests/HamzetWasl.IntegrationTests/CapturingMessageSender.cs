using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using HamzetWasl.Application.Interfaces;

namespace HamzetWasl.IntegrationTests;

/// <summary>
/// Test transport that captures the outgoing OTP messages so integration tests can
/// complete the email/phone verification steps. Production uses SES/SMS gateways.
/// </summary>
public sealed class CapturingMessageSender : IEmailSender, ISmsSender
{
    private static readonly Regex StrongCodePattern = new(@"<strong>\s*(\d{4,8})\s*</strong>", RegexOptions.Compiled);
    private static readonly Regex CodePattern = new(@"\b(\d{4,8})\b", RegexOptions.Compiled);

    private readonly ConcurrentQueue<(string Channel, string Target, string Body)> _messages = new();

    public IReadOnlyList<string> Codes => _messages
        .SelectMany(m => ExtractCodes(m.Body))
        .ToList();

    private static IEnumerable<string> ExtractCodes(string body)
    {
        var strong = StrongCodePattern.Match(body);
        if (strong.Success)
        {
            yield return strong.Groups[1].Value;
            yield break;
        }

        var match = CodePattern.Match(body);
        if (match.Success)
        {
            yield return match.Groups[1].Value;
        }
    }

    public string? LastCode => Codes.LastOrDefault();

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        _messages.Enqueue(("email", to, htmlBody));
        return Task.CompletedTask;
    }

    Task ISmsSender.SendAsync(string phone, string message, CancellationToken ct)
    {
        _messages.Enqueue(("sms", phone, message));
        return Task.CompletedTask;
    }
}
