using System.Security.Cryptography;
using System.Text;
using HamzetWasl.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace HamzetWasl.Infrastructure.Services;

/// <summary>
/// Encrypts the storage key written to VERIFICATION_EVIDENCE.encrypted_storage_key.
/// The file itself remains in a private folder and is only reachable through a
/// short-lived signed URL.
/// </summary>
public sealed class StorageKeyProtector
{
    private readonly byte[] _key;

    public StorageKeyProtector(IOptions<StorageOptions> options)
    {
        var secret = options.Value.SigningKey;
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            throw new InvalidOperationException(
                "Storage:SigningKey is required and must be at least 32 characters. " +
                "Set it through the environment variable Storage__SigningKey.");
        }

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    public string Protect(string key)
    {
        var plain = Encoding.UTF8.GetBytes(key);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var cipher = new byte[plain.Length];
        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, plain, cipher, tag);
        var payload = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, payload, nonce.Length + tag.Length, cipher.Length);
        return Convert.ToBase64String(payload);
    }

    public string Unprotect(string protectedKey)
    {
        var payload = Convert.FromBase64String(protectedKey);
        if (payload.Length < 28) throw new CryptographicException("Invalid protected key.");
        var nonce = payload.AsSpan(0, 12);
        var tag = payload.AsSpan(12, 16);
        var cipher = payload.AsSpan(28);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, 16);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
