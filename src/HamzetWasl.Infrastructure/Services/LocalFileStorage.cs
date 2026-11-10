using System.Security.Cryptography;
using System.Text;
using HamzetWasl.Application.Common;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Application.Metadata;
using HamzetWasl.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HamzetWasl.Infrastructure.Services;

public sealed class LocalFileStorage : IFileStorage
{
    private readonly StorageOptions _options;
    private readonly string _root;
    private readonly IClock _clock;

    public LocalFileStorage(IOptions<StorageOptions> options, IHostEnvironment environment, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
        _root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, _options.RootPath));
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFile> SaveAsync(FileUpload file, FileCategory category, Guid ownerId, CancellationToken ct = default)
    {
        var rule = FileRules.For(category);
        if (file.Length <= 0)
        {
            throw AppException.Validation(new Dictionary<string, string[]> { ["file"] = ["الملف فارغ."] });
        }

        if (file.Length > rule.MaxBytes)
        {
            throw new AppException(413, ErrorCodes.FileTooLarge);
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!rule.Extensions.Contains(extension) || !rule.ContentTypes.Contains(file.ContentType))
        {
            throw new AppException(400, ErrorCodes.FileTypeNotAllowed);
        }

        using var buffer = new MemoryStream();
        await file.Content.CopyToAsync(buffer, ct);
        if (buffer.Length != file.Length)
        {
            throw new AppException(400, ErrorCodes.ValidationError);
        }

        buffer.Position = 0;
        var header = new byte[16];
        var read = await buffer.ReadAsync(header, ct);
        if (!FileRules.MatchesMagicBytes(category, header.AsSpan(0, read)))
        {
            throw new AppException(400, ErrorCodes.FileTypeNotAllowed);
        }

        var key = $"{Folder(category)}/{ownerId:N}/{Guid.NewGuid():N}{extension}";
        var path = GetPath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using (var target = File.Create(path))
        {
            buffer.Position = 0;
            await buffer.CopyToAsync(target, ct);
        }

        return new StoredFile(key, file.FileName, file.ContentType, file.Length);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var path = GetPath(key);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
            // Deletion is best-effort; retention cleanup will retry.
        }

        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
    {
        var path = GetPath(key);
        if (!File.Exists(path)) return Task.FromResult<Stream?>(null);
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public string GetPublicUrl(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("public/", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return _options.PublicBaseUrl.TrimEnd('/') + "/" + key["public/".Length..];
    }

    public SignedFileUrl CreateSignedUrl(string key, TimeSpan lifetime)
    {
        var expires = _clock.UtcNow.Add(lifetime);
        var expiresUnix = new DateTimeOffset(expires).ToUnixTimeSeconds();
        var signature = Sign(key, expiresUnix);
        var url = $"/api/v1/files?key={Uri.EscapeDataString(key)}&expires={expiresUnix}&signature={Uri.EscapeDataString(signature)}";
        return new SignedFileUrl(url, expires);
    }

    public bool ValidateSignedUrl(string key, long expiresUnix, string signature)
    {
        if (expiresUnix < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return false;
        var expected = Encoding.UTF8.GetBytes(Sign(key, expiresUnix));
        var provided = Encoding.UTF8.GetBytes(signature);
        return expected.Length == provided.Length && CryptographicOperations.FixedTimeEquals(expected, provided);
    }

    private string Sign(string key, long expiresUnix)
    {
        var secret = string.IsNullOrWhiteSpace(_options.SigningKey) ? "hamzet-wasl-dev-file-key" : _options.SigningKey;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{key}|{expiresUnix}"));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string Folder(FileCategory category) => category switch
    {
        FileCategory.PublicImage => "public",
        FileCategory.OrganizationDocument => "organization-documents",
        FileCategory.VerificationEvidence => "verification-evidence",
        FileCategory.Audio => "audio",
        FileCategory.Video => "video",
        _ => "other"
    };

    private string GetPath(string key)
    {
        var relative = key.Replace('/', Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(_root, relative));
        var rootPrefix = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid storage key.");
        }

        return path;
    }
}
