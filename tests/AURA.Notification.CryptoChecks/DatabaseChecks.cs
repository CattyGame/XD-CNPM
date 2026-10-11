using System.Security.Cryptography;
using AURA.Notification.Api;
using Microsoft.Extensions.Configuration;
using Npgsql;

public static class DatabaseChecks
{
    public static async Task<int> RunAsync()
    {
        const string projectId = "crypto-storage-check";
        const string firebaseUid = "synthetic-storage-test-user";

        var deviceId = Guid.NewGuid();
        var firstToken = $"fake-first-token-{Guid.NewGuid():N}";
        var secondToken = $"fake-second-token-{Guid.NewGuid():N}";

        var connectionString = Environment.GetEnvironmentVariable(
            "AURA_CRYPTO_CHECK_CONNECTION");

        var encryptionKey = Environment.GetEnvironmentVariable(
            "AURA_CRYPTO_CHECK_KEY");

        var keyId = Environment.GetEnvironmentVariable(
            "AURA_CRYPTO_CHECK_KEY_ID");

        if (
            string.IsNullOrWhiteSpace(connectionString) ||
            string.IsNullOrWhiteSpace(encryptionKey) ||
            string.IsNullOrWhiteSpace(keyId))
        {
            Console.Error.WriteLine(
                "FAIL: Thiếu cấu hình kiểm tra PostgreSQL hoặc khóa mã hóa.");

            return 1;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notification:EncryptionKey"] = encryptionKey,
                ["Notification:EncryptionKeyId"] = keyId
            })
            .Build();

        await using var dataSource =
            NpgsqlDataSource.Create(connectionString);

        using var cipher = new TokenCipher(configuration);

        var repository = new DeviceRegistrationRepository(
            dataSource,
            cipher);

        var result = 1;

        try
        {
            var registered = await repository.RegisterAsync(
                projectId,
                firebaseUid,
                deviceId,
                firstToken,
                CancellationToken.None);

            Require(
                registered == RegistrationOutcome.Registered,
                "Không đăng ký được thiết bị kiểm thử.");

            var first = await ReadAsync(
                dataSource,
                projectId,
                deviceId);

            Require(
                first.FirebaseUid == firebaseUid,
                "UID lưu trong database không khớp.");

            Require(
                cipher.Decrypt(
                    first.Encrypted,
                    projectId,
                    deviceId,
                    first.FirebaseUid) == firstToken,
                "Token đọc từ database không giải mã đúng.");

            Require(
                CryptographicOperations.FixedTimeEquals(
                    first.Hash,
                    TokenCipher.Fingerprint(firstToken)),
                "Fingerprint token đầu tiên không khớp.");

            Console.WriteLine(
                "PASS: token đọc từ PostgreSQL giải mã đúng và khớp fingerprint.");

            var updated = await repository.RegisterAsync(
                projectId,
                firebaseUid,
                deviceId,
                secondToken,
                CancellationToken.None);

            Require(
                updated == RegistrationOutcome.Registered,
                "Không cập nhật được token.");

            var second = await ReadAsync(
                dataSource,
                projectId,
                deviceId);

            Require(
                cipher.Decrypt(
                    second.Encrypted,
                    projectId,
                    deviceId,
                    second.FirebaseUid) == secondToken,
                "Database chưa lưu token mới đúng.");

            Require(
                CryptographicOperations.FixedTimeEquals(
                    second.Hash,
                    TokenCipher.Fingerprint(secondToken)),
                "Fingerprint token mới không khớp.");

            Require(
                !first.Encrypted.Nonce.AsSpan()
                    .SequenceEqual(second.Encrypted.Nonce),
                "Cập nhật token đã dùng lại nonce cũ.");

            Console.WriteLine(
                "PASS: cập nhật cùng thiết bị lưu token mới, hash mới và nonce mới.");

            Require(
                await CountAsync(dataSource, projectId, deviceId) == 1,
                "Cập nhật đã tạo sai số lượng bản ghi.");

            Console.WriteLine(
                "PASS: cập nhật token vẫn chỉ có một bản ghi thiết bị.");

            await repository.DeleteIfTokenMatchesAsync(
                projectId,
                firebaseUid,
                deviceId,
                firstToken,
                CancellationToken.None);

            Require(
                await repository.GetTokenAsync(
                    projectId,
                    firebaseUid,
                    deviceId,
                    CancellationToken.None) == secondToken,
                "Phản hồi hết hiệu lực của token cũ đã xóa token mới.");

            Console.WriteLine(
                "PASS: token cũ không xóa registration đã cập nhật token mới.");

            await repository.DeleteIfTokenMatchesAsync(
                "another-test-project",
                firebaseUid,
                deviceId,
                secondToken,
                CancellationToken.None);

            Require(
                await CountAsync(dataSource, projectId, deviceId) == 1,
                "Sai project vẫn xóa được registration.");

            Console.WriteLine(
                "PASS: sai project không xóa được registration.");

            await repository.DeleteIfTokenMatchesAsync(
                projectId,
                "another-test-user",
                deviceId,
                secondToken,
                CancellationToken.None);

            Require(
                await CountAsync(dataSource, projectId, deviceId) == 1,
                "Sai UID vẫn xóa được registration.");

            Console.WriteLine(
                "PASS: sai UID không xóa được registration.");

            await repository.DeleteIfTokenMatchesAsync(
                projectId,
                firebaseUid,
                Guid.NewGuid(),
                secondToken,
                CancellationToken.None);

            Require(
                await CountAsync(dataSource, projectId, deviceId) == 1,
                "Sai device vẫn xóa được registration.");

            Console.WriteLine(
                "PASS: sai device không xóa được registration.");

            await repository.DeleteIfTokenMatchesAsync(
                projectId,
                firebaseUid,
                deviceId,
                secondToken,
                CancellationToken.None);

            Require(
                await CountAsync(dataSource, projectId, deviceId) == 0,
                "Token khớp nhưng registration chưa được xóa.");

            Console.WriteLine(
                "PASS: token khớp xóa được registration.");

            await repository.DeleteIfTokenMatchesAsync(
                projectId,
                firebaseUid,
                deviceId,
                secondToken,
                CancellationToken.None);

            Require(
                await CountAsync(dataSource, projectId, deviceId) == 0,
                "Xóa lặp có kết quả không đúng.");

            Console.WriteLine(
                "PASS: xóa có điều kiện lặp không lỗi.");

            result = 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"FAIL: kiểm tra PostgreSQL — {exception.GetType().Name}");

            result = 1;
        }
        finally
        {
            try
            {
                await repository.DeleteAsync(
                    projectId,
                    firebaseUid,
                    deviceId,
                    CancellationToken.None);

                Require(
                    await CountAsync(dataSource, projectId, deviceId) == 0,
                    "Bản ghi kiểm thử chưa được xóa.");

                Console.WriteLine(
                    "PASS: đã xóa và xác nhận không còn bản ghi kiểm thử.");
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    $"FAIL: dọn thiết bị {deviceId:D} — " +
                    exception.GetType().Name);

                result = 1;
            }
        }

        return result;
    }

    private static async Task<StoredToken> ReadAsync(
        NpgsqlDataSource dataSource,
        string projectId,
        Guid deviceId)
    {
        const string sql = """
            SELECT
                firebase_uid,
                token_hash,
                token_ciphertext,
                token_nonce,
                token_tag,
                encryption_key_id
            FROM notification.device_registrations
            WHERE project_id = @project_id
              AND device_id = @device_id;
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("device_id", deviceId);

        await using var reader = await command.ExecuteReaderAsync();

        Require(
            await reader.ReadAsync(),
            "Không tìm thấy bản ghi kiểm thử.");

        return new StoredToken(
            reader.GetString(0),
            reader.GetFieldValue<byte[]>(1),
            new EncryptedToken(
                reader.GetFieldValue<byte[]>(2),
                reader.GetFieldValue<byte[]>(3),
                reader.GetFieldValue<byte[]>(4),
                reader.GetString(5)));
    }

    private static async Task<long> CountAsync(
        NpgsqlDataSource dataSource,
        string projectId,
        Guid deviceId)
    {
        const string sql = """
            SELECT count(*)
            FROM notification.device_registrations
            WHERE project_id = @project_id
              AND device_id = @device_id;
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("device_id", deviceId);

        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record StoredToken(
        string FirebaseUid,
        byte[] Hash,
        EncryptedToken Encrypted);
}
