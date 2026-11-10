using HamzetWasl.Application.Interfaces;
using HamzetWasl.Application.Metadata;
using Xunit;

namespace HamzetWasl.UnitTests;

public class FileRulesTests
{
    private static readonly byte[] Png =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52
    ];

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];

    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34];

    private static readonly byte[] Mp3 = [0x49, 0x44, 0x33, 0x03, 0x00, 0x00, 0x00, 0x00];
    private static readonly byte[] Mp3Frame = [0xFF, 0xFB, 0x90, 0x00];
    private static readonly byte[] WebM = [0x1A, 0x45, 0xDF, 0xA3];
    private static readonly byte[] Ogg = [0x4F, 0x67, 0x67, 0x53];

    private static readonly byte[] Text = [0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x21];

    [Fact]
    public void Images_up_to_5_mb_are_allowed_for_public_assets()
    {
        var rule = FileRules.For(FileCategory.PublicImage);
        Assert.Equal(5 * 1024 * 1024, rule.MaxBytes);
        Assert.Contains("image/png", rule.ContentTypes);
        Assert.Contains("image/jpeg", rule.ContentTypes);
        Assert.DoesNotContain("application/pdf", rule.ContentTypes);
    }

    [Fact]
    public void Organization_documents_accept_pdf_or_image_up_to_10_mb()
    {
        var rule = FileRules.For(FileCategory.OrganizationDocument);
        Assert.Equal(10 * 1024 * 1024, rule.MaxBytes);
        Assert.Contains(".pdf", rule.Extensions);
        Assert.Contains(".png", rule.Extensions);
        Assert.Contains(".jpg", rule.Extensions);
    }

    [Fact]
    public void Verification_evidence_is_limited_to_images()
    {
        var rule = FileRules.For(FileCategory.VerificationEvidence);
        Assert.Contains("image/png", rule.ContentTypes);
        Assert.Contains("image/jpeg", rule.ContentTypes);
        Assert.DoesNotContain("application/pdf", rule.ContentTypes);
    }

    [Theory]
    [InlineData(FileCategory.PublicImage, true)]
    [InlineData(FileCategory.OrganizationDocument, true)]
    [InlineData(FileCategory.VerificationEvidence, true)]
    [InlineData(FileCategory.Audio, false)]
    public void Png_magic_bytes_recognised(FileCategory category, bool expected) =>
        Assert.Equal(expected, FileRules.MatchesMagicBytes(category, Png));

    [Theory]
    [InlineData(FileCategory.PublicImage, true)]
    [InlineData(FileCategory.OrganizationDocument, true)]
    [InlineData(FileCategory.Audio, false)]
    public void Jpeg_magic_bytes_recognised(FileCategory category, bool expected) =>
        Assert.Equal(expected, FileRules.MatchesMagicBytes(category, Jpeg));

    [Fact]
    public void Pdf_is_only_valid_for_organization_documents()
    {
        Assert.True(FileRules.MatchesMagicBytes(FileCategory.OrganizationDocument, Pdf));
        Assert.False(FileRules.MatchesMagicBytes(FileCategory.PublicImage, Pdf));
        Assert.False(FileRules.MatchesMagicBytes(FileCategory.VerificationEvidence, Pdf));
    }

    [Fact]
    public void Mp3_and_audio_containers_are_recognised()
    {
        Assert.True(FileRules.MatchesMagicBytes(FileCategory.Audio, Mp3));
        Assert.True(FileRules.MatchesMagicBytes(FileCategory.Audio, Mp3Frame));
        Assert.True(FileRules.MatchesMagicBytes(FileCategory.Audio, WebM));
        Assert.True(FileRules.MatchesMagicBytes(FileCategory.Audio, Ogg));
        Assert.False(FileRules.MatchesMagicBytes(FileCategory.PublicImage, Mp3));
    }

    [Fact]
    public void Text_files_are_never_accepted()
    {
        foreach (var category in new[] { FileCategory.PublicImage, FileCategory.OrganizationDocument, FileCategory.VerificationEvidence, FileCategory.Audio, FileCategory.Video })
        {
            Assert.False(FileRules.MatchesMagicBytes(category, Text));
        }
    }

    [Fact]
    public void A_short_or_empty_header_is_rejected()
    {
        Assert.False(FileRules.MatchesMagicBytes(FileCategory.PublicImage, ReadOnlySpan<byte>.Empty));
        Assert.False(FileRules.MatchesMagicBytes(FileCategory.PublicImage, new byte[] { 0x89 }));
    }
}
