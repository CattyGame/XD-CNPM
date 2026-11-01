using System.Security.Cryptography;
using Npgsql;

namespace AURA.Notification.Api;

public enum RegistrationOutcome
{
    Registered,
    Conflict
}

public sealed record ActiveDeviceToken(Guid DeviceId, string Token);

public sealed class DeviceRegistrationRepository
{
    private readonly NpgsqlDataSource dataSource;
    private readonly TokenCipher cipher;

    public DeviceRegistrationRepository(
        NpgsqlDataSource dataSource,
        TokenCipher cipher)
    {
        this.dataSource = dataSource;
        this.cipher = cipher;
    }

    public async Task<RegistrationOutcome> RegisterAsync(
        string projectId,
        string firebaseUid,
        Guid deviceId,
        string token,
        CancellationToken cancellationToken)
    {
        var fingerprint = TokenCipher.Fingerprint(token);

        var encrypted = cipher.Encrypt(
            token,
            projectId,
            deviceId,
            firebaseUid);

        const string sql = """
            INSERT INTO notification.device_registrations AS existing (
                project_id,
                device_id,
                firebase_uid,
                token_hash,
                token_ciphertext,
                token_nonce,
                token_tag,
                encryption_key_id,
                platform
            )
            VALUES (
                @project_id,
                @device_id,
                @firebase_uid,
                @token_hash,
                @token_ciphertext,
                @token_nonce,
                @token_tag,
                @encryption_key_id,
                'web'
            )
            ON CONFLICT (project_id, device_id)
            DO UPDATE SET
                token_hash = EXCLUDED.token_hash,
                token_ciphertext = EXCLUDED.token_ciphertext,
                token_nonce = EXCLUDED.token_nonce,
                token_tag = EXCLUDED.token_tag,
                encryption_key_id = EXCLUDED.encryption_key_id,
                updated_at = CURRENT_TIMESTAMP
            WHERE existing.firebase_uid = EXCLUDED.firebase_uid
            RETURNING device_id;
            """;

        await using var command = dataSource.CreateCommand(sql);

        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("device_id", deviceId);
        command.Parameters.AddWithValue("firebase_uid", firebaseUid);
        command.Parameters.AddWithValue("token_hash", fingerprint);
        command.Parameters.AddWithValue(
            "token_ciphertext",
            encrypted.Ciphertext);
        command.Parameters.AddWithValue("token_nonce", encrypted.Nonce);
        command.Parameters.AddWithValue("token_tag", encrypted.Tag);
        command.Parameters.AddWithValue(
            "encryption_key_id",
            encrypted.KeyId);

        try
        {
            var result = await command.ExecuteScalarAsync(
                cancellationToken);

            return result is Guid
                ? RegistrationOutcome.Registered
                : RegistrationOutcome.Conflict;
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return RegistrationOutcome.Conflict;
        }
    }

    public async Task DeleteAsync(
        string projectId,
        string firebaseUid,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            DELETE FROM notification.device_registrations
            WHERE project_id = @project_id
              AND firebase_uid = @firebase_uid
              AND device_id = @device_id;
            """;

        await using var command = dataSource.CreateCommand(sql);

        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("firebase_uid", firebaseUid);
        command.Parameters.AddWithValue("device_id", deviceId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteIfTokenMatchesAsync(
        string projectId,
        string firebaseUid,
        Guid deviceId,
        string token,
        CancellationToken cancellationToken)
    {
        const string sql = """
            DELETE FROM notification.device_registrations
            WHERE project_id = @project_id
              AND firebase_uid = @firebase_uid
              AND device_id = @device_id
              AND token_hash = @token_hash;
            """;

        await using var command = dataSource.CreateCommand(sql);

        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("firebase_uid", firebaseUid);
        command.Parameters.AddWithValue("device_id", deviceId);
        command.Parameters.AddWithValue(
            "token_hash",
            TokenCipher.Fingerprint(token));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<string?> GetTokenAsync(
        string projectId,
        string firebaseUid,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                token_ciphertext,
                token_nonce,
                token_tag,
                encryption_key_id,
                token_hash
            FROM notification.device_registrations
            WHERE project_id = @project_id
              AND firebase_uid = @firebase_uid
              AND device_id = @device_id;
            """;

        await using var command = dataSource.CreateCommand(sql);

        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("firebase_uid", firebaseUid);
        command.Parameters.AddWithValue("device_id", deviceId);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var encrypted = new EncryptedToken(
            reader.GetFieldValue<byte[]>(0),
            reader.GetFieldValue<byte[]>(1),
            reader.GetFieldValue<byte[]>(2),
            reader.GetString(3));

        var storedFingerprint = reader.GetFieldValue<byte[]>(4);

        var token = cipher.Decrypt(
            encrypted,
            projectId,
            deviceId,
            firebaseUid);

        var actualFingerprint = TokenCipher.Fingerprint(token);

        if (!CryptographicOperations.FixedTimeEquals(
            storedFingerprint,
            actualFingerprint))
        {
            throw new CryptographicException(
                "Stored token fingerprint does not match.");
        }

        return token;
    }

    public async Task<List<ActiveDeviceToken>> GetTokensForUserAsync(
        string projectId,
        string firebaseUid,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                device_id,
                token_ciphertext,
                token_nonce,
                token_tag,
                encryption_key_id,
                token_hash
            FROM notification.device_registrations
            WHERE project_id = @project_id
              AND firebase_uid = @firebase_uid;
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("firebase_uid", firebaseUid);

        var list = new List<ActiveDeviceToken>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            try
            {
                var deviceId = reader.GetGuid(0);
                var encrypted = new EncryptedToken(
                    reader.GetFieldValue<byte[]>(1),
                    reader.GetFieldValue<byte[]>(2),
                    reader.GetFieldValue<byte[]>(3),
                    reader.GetString(4));

                var storedFingerprint = reader.GetFieldValue<byte[]>(5);

                var token = cipher.Decrypt(
                    encrypted,
                    projectId,
                    deviceId,
                    firebaseUid);

                var actualFingerprint = TokenCipher.Fingerprint(token);
                if (CryptographicOperations.FixedTimeEquals(storedFingerprint, actualFingerprint))
                {
                    list.Add(new ActiveDeviceToken(deviceId, token));
                }
            }
            catch
            {
                // Skip invalid/unreadable token
            }
        }

        return list;
    }

    public async Task<bool> CanConnectAsync(
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = 'notification'
                  AND table_name = 'device_registrations'
            );
            """;

        await using var command = dataSource.CreateCommand(sql);

        return await command.ExecuteScalarAsync(cancellationToken)
            is true;
    }
}