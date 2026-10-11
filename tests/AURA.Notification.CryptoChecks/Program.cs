using System.Security.Cryptography;
using AURA.Notification.Api;
using Microsoft.Extensions.Configuration;

if (args.Contains("--database"))
{
    return await DatabaseChecks.RunAsync();
}

const string projectId = "crypto-test-project";
const string firebaseUid = "crypto-test-user";
const string token = "fake-fcm-token-kiểm-thử";

var deviceId = Guid.NewGuid();
var key = RandomNumberGenerator.GetBytes(32);
var otherKey = RandomNumberGenerator.GetBytes(32);

try
{
    using var cipher = CreateCipher(key, "test-v1");
    using var wrongKeyCipher = CreateCipher(otherKey, "test-v1");

    var encrypted = cipher.Encrypt(
        token,
        projectId,
        deviceId,
        firebaseUid);

    var passed = 0;

    void Check(string name, Action action)
    {
        action();
        passed++;
        Console.WriteLine($"PASS: {name}");
    }

    Check("Giải mã đúng trả lại token ban đầu, giữ Unicode", () =>
    {
        var decrypted = cipher.Decrypt(
            encrypted,
            projectId,
            deviceId,
            firebaseUid);

        Require(decrypted == token, "Token giải mã không khớp.");
    });

    Check("Mã hóa lại dùng nonce khác và vẫn giải mã được", () =>
    {
        var second = cipher.Encrypt(
            token,
            projectId,
            deviceId,
            firebaseUid);

        Require(
            !encrypted.Nonce.AsSpan().SequenceEqual(second.Nonce),
            "Hai lần mã hóa dùng cùng nonce.");

        Require(
            cipher.Decrypt(
                second,
                projectId,
                deviceId,
                firebaseUid) == token,
            "Lần mã hóa thứ hai không giải mã được.");
    });

    Check("Sai project bị từ chối", () =>
    {
        ExpectRejected<CryptographicException>(() =>
        {
            cipher.Decrypt(
                encrypted,
                "another-project",
                deviceId,
                firebaseUid);
        });
    });

    Check("Sai device bị từ chối", () =>
    {
        ExpectRejected<CryptographicException>(() =>
        {
            cipher.Decrypt(
                encrypted,
                projectId,
                Guid.NewGuid(),
                firebaseUid);
        });
    });

    Check("Sai UID bị từ chối", () =>
    {
        ExpectRejected<CryptographicException>(() =>
        {
            cipher.Decrypt(
                encrypted,
                projectId,
                deviceId,
                "another-user");
        });
    });

    Check("Ciphertext bị sửa bị từ chối", () =>
    {
        var tampered = encrypted with
        {
            Ciphertext = FlipFirstByte(encrypted.Ciphertext)
        };

        ExpectRejected<CryptographicException>(() =>
        {
            cipher.Decrypt(
                tampered,
                projectId,
                deviceId,
                firebaseUid);
        });
    });

    Check("Authentication tag bị sửa bị từ chối", () =>
    {
        var tampered = encrypted with
        {
            Tag = FlipFirstByte(encrypted.Tag)
        };

        ExpectRejected<CryptographicException>(() =>
        {
            cipher.Decrypt(
                tampered,
                projectId,
                deviceId,
                firebaseUid);
        });
    });

    Check("Nonce bị sửa bị từ chối", () =>
    {
        var tampered = encrypted with
        {
            Nonce = FlipFirstByte(encrypted.Nonce)
        };

        ExpectRejected<CryptographicException>(() =>
        {
            cipher.Decrypt(
                tampered,
                projectId,
                deviceId,
                firebaseUid);
        });
    });

    Check("Sai khóa dù cùng key ID bị từ chối", () =>
    {
        ExpectRejected<CryptographicException>(() =>
        {
            wrongKeyCipher.Decrypt(
                encrypted,
                projectId,
                deviceId,
                firebaseUid);
        });
    });

    Check("Key ID không có bị từ chối", () =>
    {
        var unavailableKey = encrypted with
        {
            KeyId = "unavailable-key"
        };

        ExpectRejected<CryptographicException>(() =>
        {
            cipher.Decrypt(
                unavailableKey,
                projectId,
                deviceId,
                firebaseUid);
        });
    });

    Check("Nonce sai độ dài bị từ chối", () =>
    {
        var malformed = encrypted with
        {
            Nonce = new byte[11]
        };

        ExpectRejected<CryptographicException>(() =>
        {
            cipher.Decrypt(
                malformed,
                projectId,
                deviceId,
                firebaseUid);
        });
    });

    Check("Token rỗng bị từ chối", () =>
    {
        ExpectRejected<ArgumentException>(() =>
        {
            cipher.Encrypt(
                "",
                projectId,
                deviceId,
                firebaseUid);
        });
    });

    Console.WriteLine($"PASS: {passed}/12 kiểm tra mã hóa.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"FAIL: {exception.Message}");
    return 1;
}
finally
{
    CryptographicOperations.ZeroMemory(key);
    CryptographicOperations.ZeroMemory(otherKey);
}

static TokenCipher CreateCipher(byte[] key, string keyId)
{
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Notification:EncryptionKey"] =
                Convert.ToBase64String(key),
            ["Notification:EncryptionKeyId"] = keyId
        })
        .Build();

    return new TokenCipher(configuration);
}

static byte[] FlipFirstByte(byte[] source)
{
    var copy = (byte[])source.Clone();
    copy[0] ^= 1;
    return copy;
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void ExpectRejected<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(
        $"Mong đợi {typeof(TException).Name} nhưng thao tác được chấp nhận.");
}