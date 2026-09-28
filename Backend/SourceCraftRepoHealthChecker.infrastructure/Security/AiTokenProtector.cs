using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SourceCraftRepoHealthChecker.Application.Security.Interfaces;
using SourceCraftRepoHealthChecker.infrastructure.Options;

namespace SourceCraftRepoHealthChecker.infrastructure.Security;

public sealed class AiTokenProtector(IOptions<AiTokenEncryptionOptions> options) : IAiTokenProtector, ISecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key = ResolveKey(options.Value.Key);

    private static byte[] ResolveKey(string configuredKey)
    {
        if (string.IsNullOrWhiteSpace(configuredKey))
            throw new InvalidOperationException("AiTokenEncryptionOptions.Key is not configured. Set a base64-encoded 32-byte key via configuration or secrets.");

        var key = Convert.FromBase64String(configuredKey);
        if (key.Length is not (16 or 24 or 32))
            throw new InvalidOperationException("AiTokenEncryptionOptions.Key must be a base64 key of 16, 24 or 32 bytes.");

        return key;
    }

    public string Protect(string token)
    {
        var plaintext = Encoding.UTF8.GetBytes(token);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var payload = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, payload, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, payload, NonceSize + TagSize, ciphertext.Length);

        return Convert.ToBase64String(payload);
    }

    public string Unprotect(string protectedToken)
    {
        var payload = Convert.FromBase64String(protectedToken);
        if (payload.Length < NonceSize + TagSize)
            throw new CryptographicException("Защищённый токен повреждён.");

        var nonce = payload.AsSpan(0, NonceSize);
        var tag = payload.AsSpan(NonceSize, TagSize);
        var ciphertext = payload.AsSpan(NonceSize + TagSize);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
