using HamzetWasl.Application.Interfaces;

namespace HamzetWasl.Application.Metadata;

public sealed record FileRule(
    IReadOnlySet<string> ContentTypes,
    IReadOnlySet<string> Extensions,
    long MaxBytes);

public static class FileRules
{
    private const long Mb = 1024 * 1024;

    private static readonly IReadOnlySet<string> ImageTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg"
    };

    private static readonly IReadOnlySet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg"
    };

    public static FileRule For(FileCategory category) => category switch
    {
        FileCategory.PublicImage => new(ImageTypes, ImageExtensions, 5 * Mb),
        FileCategory.OrganizationDocument => new(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "application/pdf", "image/png", "image/jpeg" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf", ".png", ".jpg", ".jpeg" },
            10 * Mb),
        FileCategory.VerificationEvidence => new(ImageTypes, ImageExtensions, 10 * Mb),
        FileCategory.Audio => new(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "audio/mpeg", "audio/mp4", "audio/ogg", "audio/wav", "audio/webm" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp3", ".m4a", ".ogg", ".wav", ".webm" },
            20 * Mb),
        FileCategory.Video => new(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "video/mp4", "video/webm" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".webm" },
            100 * Mb),
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };

    public static bool MatchesMagicBytes(FileCategory category, ReadOnlySpan<byte> header)
    {
        if (header.Length >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            return category is FileCategory.PublicImage or FileCategory.OrganizationDocument or FileCategory.VerificationEvidence;
        }

        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return category is FileCategory.PublicImage or FileCategory.OrganizationDocument or FileCategory.VerificationEvidence;
        }

        if (header.Length >= 5 &&
            header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46 && header[4] == 0x2D)
        {
            return category == FileCategory.OrganizationDocument;
        }

        if (header.Length >= 3 && header[0] == 0x49 && header[1] == 0x44 && header[2] == 0x33)
        {
            return category == FileCategory.Audio;
        }

        if (header.Length >= 2 && header[0] == 0xFF && header[1] is 0xFB or 0xF3 or 0xF2 or 0xFA)
        {
            return category == FileCategory.Audio;
        }

        // Audio/video containers: do a conservative signature check for the
        // formats the API accepts. MP4/M4A share the ftyp box; WebM/Matroska
        // start with the EBML header; OGG starts with OggS; WAV with RIFF.
        if (category is FileCategory.Audio or FileCategory.Video)
        {
            if (header.Length >= 4 && header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3) return true;
            if (header.Length >= 4 && header[0] == 0x4F && header[1] == 0x67 && header[2] == 0x67 && header[3] == 0x53) return true;
            if (header.Length >= 4 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46) return true;
            if (header.Length >= 12 &&
                header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70) return true;
        }

        return false;
    }
}
