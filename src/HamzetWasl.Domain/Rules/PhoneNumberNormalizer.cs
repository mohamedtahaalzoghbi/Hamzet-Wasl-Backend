using System.Text.RegularExpressions;

namespace HamzetWasl.Domain.Rules;

/// <summary>Egyptian mobile normalisation to E.164 (+20...).</summary>
public static partial class PhoneNumberNormalizer
{
    [GeneratedRegex(@"^\+201[0125]\d{8}$", RegexOptions.CultureInvariant)]
    private static partial Regex EgyptianMobileRegex();

    /// <summary>
    /// Returns a normalised E.164 number, or null when the value is not a valid
    /// Egyptian mobile number. Never stores local-format numbers.
    /// </summary>
    public static string? TryNormalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());

        if (digits.StartsWith("0020", StringComparison.Ordinal) && digits.Length == 14)
        {
            digits = digits[2..];
        }

        if (digits.StartsWith("20", StringComparison.Ordinal) && digits.Length == 12)
        {
            digits = "0" + digits[2..];
        }

        if (digits.Length == 10 && digits[0] == '1')
        {
            digits = "0" + digits;
        }

        if (digits.Length != 11 || digits[0] != '0')
        {
            return null;
        }

        var e164 = "+20" + digits[1..];
        return EgyptianMobileRegex().IsMatch(e164) ? e164 : null;
    }

    public static string NormalizeOrThrow(string value) =>
        TryNormalize(value) ?? throw new ArgumentException("رقم الهاتف غير صحيح.", nameof(value));
}
