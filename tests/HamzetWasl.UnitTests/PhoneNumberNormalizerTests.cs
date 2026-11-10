using HamzetWasl.Domain.Rules;
using Xunit;

namespace HamzetWasl.UnitTests;

public class PhoneNumberNormalizerTests
{
    [Theory]
    [InlineData("01012345678", "+201012345678")]
    [InlineData("+201012345678", "+201012345678")]
    [InlineData("201012345678", "+201012345678")]
    [InlineData("00201012345678", "+201012345678")]
    [InlineData("+20 101 234 5678", "+201012345678")]
    [InlineData("(010) 1234-5678", "+201012345678")]
    [InlineData("01012345678#", "+201012345678")]
    [InlineData("01212345678", "+201212345678")]
    [InlineData("01112345678", "+201112345678")]
    [InlineData("01512345678", "+201512345678")]
    public void Egyptian_mobiles_normalise_to_e164(string input, string expected) =>
        Assert.Equal(expected, PhoneNumberNormalizer.TryNormalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abcdefghij")]
    [InlineData("0197")]           // Too short.
    [InlineData("0101234567")]     // 10 digits total, not 11.
    [InlineData("010123456789")]   // Too long.
    [InlineData("01712345678")]    // Third digit not 0/1/2/5 (not 010/011/012/015).
    [InlineData("01912345678")]    // Third digit 9.
    [InlineData("+221012345678")]  // Wrong country code.
    [InlineData("971501234567")]   // Non-Egyptian.
    public void Invalid_or_non_egyptian_numbers_are_rejected(string? input) =>
        Assert.Null(PhoneNumberNormalizer.TryNormalize(input));

    [Fact]
    public void NormalizeOrThrow_throws_for_invalid_input() =>
        Assert.Throws<ArgumentException>(() => PhoneNumberNormalizer.NormalizeOrThrow("01912345678"));
}
