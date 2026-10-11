using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AURA.Notification.Api;

public sealed record EncryptedToken(
    byte[] Ciphertext,
    byte[] Nonce,
    byte[] Tag,
    string KeyId);

public sealed class TokenCipher : IDisposable
{
    private readonly byte[] key;
    private readonly string keyId;

    public TokenCipher(IConfiguration configuration)
    {
        var configuredKey = configuration["Notification:EncryptionKey"];

        keyId = configuration["Notification:EncryptionKeyId"]
            ?? throw new InvalidOperationException(
                "Missing Notification:EncryptionKeyId.");

        if (string.IsNullOrWhiteSpace(keyId) || keyId.Length > 64)
        {
            throw new InvalidOperationException(
                "Invalid Notification:EncryptionKeyId.");
        }

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            throw new InvalidOperationException(
                "Missing Notification:EncryptionKey.");
        }

        try
        {
            key = Convert.FromBase64String(configuredKey);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "Notification encryption key must be Base64.");
        }

        if (key.Length != 32)
        {
            CryptographicOperations.ZeroMemory(key);

            throw new InvalidOperationException(
                "Notification encryption key must contain 32 bytes.");
        }
    }

    public static byte[] Fingerprint(string token)
    {
        ValidateToken(token);

        var bytes = Encoding.UTF8.GetBytes(token);

        try
        {
            return SHA256.HashData(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public EncryptedToken Encrypt(
        string token,
        string projectId,
        Guid deviceId,
        string firebaseUid)
    {
        ValidateToken(token);

        var plaintext = Encoding.UTF8.GetBytes(token);
        var ciphertext = new byte[plaintext.Length];
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];

        var associatedData = CreateAssociatedData(
            projectId,
            deviceId,
            firebaseUid,
            keyId);

        try
        {
            using var aes = new AesGcm(key, 16);

            aes.Encrypt(
                nonce,
                plaintext,
                ciphertext,
                tag,
                associatedData);

            return new EncryptedToken(
                ciphertext,
                nonce,
                tag,
                keyId);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public string Decrypt(
        EncryptedToken encrypted,
        string projectId,
        Guid deviceId,
        string firebaseUid)
    {
        if (!string.Equals(
            encrypted.KeyId,
            keyId,
            StringComparison.Ordinal))
        {
            throw new CryptographicException(
                "Required encryption key is unavailable.");
        }

        if (
            encrypted.Nonce.Length != 12 ||
            encrypted.Tag.Length != 16 ||
            encrypted.Ciphertext.Length is < 1 or > 4096)
        {
            throw new CryptographicException(
                "Invalid encrypted token structure.");
        }

        var plaintext = new byte[encrypted.Ciphertext.Length];

        var associatedData = CreateAssociatedData(
            projectId,
            deviceId,
            firebaseUid,
            encrypted.KeyId);

        try
        {
            using var aes = new AesGcm(key, 16);

            aes.Decrypt(
                encrypted.Nonce,
                encrypted.Ciphertext,
                encrypted.Tag,
                plaintext,
                associatedData);

            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static byte[] CreateAssociatedData(
        string projectId,
        Guid deviceId,
        string firebaseUid,
        string encryptionKeyId)
    {
        if (
            string.IsNullOrWhiteSpace(projectId) ||
            string.IsNullOrWhiteSpace(firebaseUid) ||
            deviceId == Guid.Empty)
        {
            throw new ArgumentException(
                "Token ownership context is required.");
        }

        return JsonSerializer.SerializeToUtf8Bytes(new[]
        {
            projectId,
            deviceId.ToString("D"),
            firebaseUid,
            encryptionKeyId
        });
    }

    private static void ValidateToken(string token)
    {
        if (
            string.IsNullOrWhiteSpace(token) ||
            Encoding.UTF8.GetByteCount(token) > 4096)
        {
            throw new ArgumentException(
                "Invalid FCM token length.");
        }
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(key);
    }
}