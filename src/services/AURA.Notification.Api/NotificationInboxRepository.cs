using Npgsql;
using NpgsqlTypes;

namespace AURA.Notification.Api;

public sealed class NotificationInboxRepository(
    NpgsqlDataSource dataSource,
    ILogger<NotificationInboxRepository> logger)
{
    public async Task<SaveInboxResult> SaveAsync(SaveInboxItemCommand command, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO notification.inbox_items
                (account_id, tenant_id, event_id, analysis_id, type, title, is_read, created_at, target_path, metadata)
            VALUES
                (@account_id, @tenant_id, @event_id, @analysis_id, @type, @title, false, now(), @target_path, @metadata::jsonb)
            ON CONFLICT (account_id, event_id) DO NOTHING
            RETURNING id, created_at;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, connection);

        cmd.Parameters.AddWithValue("account_id", command.AccountId);
        cmd.Parameters.AddWithValue("tenant_id", command.TenantId);
        cmd.Parameters.AddWithValue("event_id", command.EventId);
        cmd.Parameters.AddWithValue("analysis_id", command.AnalysisId);
        cmd.Parameters.AddWithValue("type", command.Type);
        cmd.Parameters.AddWithValue("title", command.Title);
        cmd.Parameters.AddWithValue("target_path", command.TargetPath);
        cmd.Parameters.AddWithValue("metadata", command.Metadata ?? "{}");

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            var id = reader.GetGuid(0);
            var createdAt = reader.GetFieldValue<DateTimeOffset>(1);
            logger.LogInformation("Inbox item {NotificationId} created for account {AccountId} from event {EventId}.",
                id, command.AccountId, command.EventId);
            return new SaveInboxResult(SaveInboxOutcome.Created, id, createdAt);
        }

        logger.LogInformation("Inbox item deduplicated for account {AccountId} and event {EventId}.",
            command.AccountId, command.EventId);

        // Deduplicated: Retrieve existing notification id and created_at
        await reader.DisposeAsync();
        const string lookupSql = "SELECT id, created_at FROM notification.inbox_items WHERE account_id = @account_id AND event_id = @event_id;";
        await using var lookupCmd = new NpgsqlCommand(lookupSql, connection);
        lookupCmd.Parameters.AddWithValue("account_id", command.AccountId);
        lookupCmd.Parameters.AddWithValue("event_id", command.EventId);
        await using var lookupReader = await lookupCmd.ExecuteReaderAsync(ct);
        if (await lookupReader.ReadAsync(ct))
        {
            return new SaveInboxResult(SaveInboxOutcome.Deduplicated, lookupReader.GetGuid(0), lookupReader.GetFieldValue<DateTimeOffset>(1));
        }

        return new SaveInboxResult(SaveInboxOutcome.Deduplicated, Guid.Empty, DateTimeOffset.UtcNow);
    }

    public async Task<InboxListResponse> GetInboxAsync(
        Guid accountId,
        int limit,
        string? cursor,
        bool unreadOnly,
        CancellationToken ct)
    {
        if (limit < 1) limit = 20;
        if (limit > 100) limit = 100;

        DateTimeOffset? cursorCreatedAt = null;
        Guid? cursorId = null;

        if (!string.IsNullOrWhiteSpace(cursor) && InboxCursor.TryDecode(cursor, out var parsedDate, out var parsedId))
        {
            cursorCreatedAt = parsedDate;
            cursorId = parsedId;
        }

        var sql = """
            SELECT id, event_id, analysis_id, type, title, is_read, created_at, read_at, target_path
            FROM notification.inbox_items
            WHERE account_id = @account_id
              AND (@unread_only = false OR is_read = false)
              AND (@cursor_created_at IS NULL OR (created_at, id) < (@cursor_created_at, @cursor_id))
            ORDER BY created_at DESC, id DESC
            LIMIT @query_limit;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, connection);

        cmd.Parameters.AddWithValue("account_id", accountId);
        cmd.Parameters.AddWithValue("unread_only", unreadOnly);
        cmd.Parameters.Add("cursor_created_at", NpgsqlDbType.TimestampTz).Value = (object?)cursorCreatedAt ?? DBNull.Value;
        cmd.Parameters.Add("cursor_id", NpgsqlDbType.Uuid).Value = (object?)cursorId ?? DBNull.Value;
        cmd.Parameters.AddWithValue("query_limit", limit + 1);

        var items = new List<InboxItemResponse>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new InboxItemResponse(
                NotificationId: reader.GetGuid(0),
                EventId: reader.GetGuid(1),
                AnalysisId: reader.GetGuid(2),
                Type: reader.GetString(3),
                Title: reader.GetString(4),
                IsRead: reader.GetBoolean(5),
                CreatedAt: reader.GetFieldValue<DateTimeOffset>(6),
                ReadAt: reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
                TargetPath: reader.GetString(8)
            ));
        }

        string? nextCursor = null;
        if (items.Count > limit)
        {
            var hasMore = items[^1];
            items.RemoveAt(items.Count - 1);
            var last = items[^1];
            nextCursor = InboxCursor.Encode(last.CreatedAt, last.NotificationId);
        }

        return new InboxListResponse(items, nextCursor);
    }

    public async Task<int> GetUnreadCountAsync(Guid accountId, CancellationToken ct)
    {
        const string sql = "SELECT COUNT(*) FROM notification.inbox_items WHERE account_id = @account_id AND is_read = false;";
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("account_id", accountId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    public async Task<MarkReadOutcome> MarkAsReadAsync(Guid notificationId, Guid accountId, CancellationToken ct)
    {
        const string updateSql = """
            UPDATE notification.inbox_items
            SET is_read = true, read_at = COALESCE(read_at, now())
            WHERE id = @id AND account_id = @account_id;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var updateCmd = new NpgsqlCommand(updateSql, connection);
        updateCmd.Parameters.AddWithValue("id", notificationId);
        updateCmd.Parameters.AddWithValue("account_id", accountId);

        var affected = await updateCmd.ExecuteNonQueryAsync(ct);
        if (affected > 0)
        {
            return MarkReadOutcome.Success;
        }

        // Check if the item exists under another account
        const string checkSql = "SELECT account_id FROM notification.inbox_items WHERE id = @id;";
        await using var checkCmd = new NpgsqlCommand(checkSql, connection);
        checkCmd.Parameters.AddWithValue("id", notificationId);
        var owner = await checkCmd.ExecuteScalarAsync(ct);

        if (owner is null)
        {
            return MarkReadOutcome.NotFound;
        }

        logger.LogWarning("Account {CallerAccountId} attempted to mark notification {NotificationId} belonging to another account.",
            accountId, notificationId);
        return MarkReadOutcome.Forbidden;
    }
}
