using HamzetWasl.Application.Common;

namespace HamzetWasl.Application.Interfaces;

public enum FileCategory
{
    PublicImage,
    OrganizationDocument,
    VerificationEvidence,
    Audio,
    Video
}

public sealed record StoredFile(string Key, string FileName, string ContentType, long Length);
public sealed record SignedFileUrl(string Url, DateTime ExpiresAt);

public interface IFileStorage
{
    Task<StoredFile> SaveAsync(FileUpload file, FileCategory category, Guid ownerId, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default);
    string GetPublicUrl(string key);
    SignedFileUrl CreateSignedUrl(string key, TimeSpan lifetime);
    bool ValidateSignedUrl(string key, long expiresUnix, string signature);
}
