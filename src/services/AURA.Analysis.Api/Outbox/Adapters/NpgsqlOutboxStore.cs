using Npgsql;
using NpgsqlTypes;

namespace AURA.Analysis.Api.Outbox.Adapters;

/// <summary>Boc rieng NpgsqlDataSource cua publisher de KHONG de len/lan voi NpgsqlDataSource (role app) ma AddAnalysisRuntime co the da dang ky.</summary>
public sealed class OutboxDataSource
{
    public OutboxDataSource(NpgsqlDataSource ds) { DataSource = ds; }
    public NpgsqlDataSource DataSource { get; }
}

/// <summary>IOutboxStore tren PostgreSQL. SQL nam o OutboxSql (da test bang psql: tests/outbox_sql_test.py).
/// Dung connection string cua role aura_analysis_publisher (rieng, quyen toi thieu), KHONG dung role app.
/// LUU Y: file nay can package Npgsql nen chua duoc bien dich trong moi truong soan thao; hay build tren may ban.</summary>
public sealed class NpgsqlOutboxStore : IOutboxStore
{
    private readonly NpgsqlDataSource _ds;
    public NpgsqlOutboxStore(OutboxDataSource source) { _ds = source.DataSource; }

    public async Task<IReadOnlyList<OutboxRecord>> ClaimAsync(OutboxClaim c, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand(OutboxSql.Claim);
        cmd.Parameters.AddWithValue("now", c.Now.UtcDateTime);
        cmd.Parameters.AddWithValue("min_occurred_at", c.MinOccurredAt.UtcDateTime);
        cmd.Parameters.Add(new NpgsqlParameter("excluded_tenants", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = c.ExcludedTenants.ToArray() });
        cmd.Parameters.AddWithValue("batch_size", c.BatchSize);
        cmd.Parameters.AddWithValue("lease_seconds", c.Lease.TotalSeconds);
        cmd.Parameters.AddWithValue("max_attempts", c.MaxAttempts);
        var list = new List<OutboxRecord>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            list.Add(new OutboxRecord(rd.GetGuid(0), rd.GetGuid(1), rd.GetString(2), rd.GetString(3), rd.GetString(4),
                new DateTimeOffset(DateTime.SpecifyKind(rd.GetDateTime(5), DateTimeKind.Utc)), rd.GetInt32(6)));
        return list.OrderBy(r => r.OccurredAt).ThenBy(r => r.Id).ToList();
    }

    public async Task<bool> MarkPublishedAsync(Guid id, DateTimeOffset at, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand(OutboxSql.MarkPublished);
        cmd.Parameters.AddWithValue("at", at.UtcDateTime); cmd.Parameters.AddWithValue("id", id);
        return await cmd.ExecuteNonQueryAsync(ct) == 1;
    }

    public async Task MarkRetryAsync(Guid id, string errorCode, DateTimeOffset at, DateTimeOffset nextPublishAt, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand(OutboxSql.MarkRetry);
        cmd.Parameters.AddWithValue("code", errorCode); cmd.Parameters.AddWithValue("at", at.UtcDateTime);
        cmd.Parameters.AddWithValue("next", nextPublishAt.UtcDateTime); cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task MarkNeedsAttentionAsync(Guid id, string errorCode, DateTimeOffset at, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand(OutboxSql.MarkNeedsAttention);
        cmd.Parameters.AddWithValue("code", errorCode); cmd.Parameters.AddWithValue("at", at.UtcDateTime); cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ReleaseUnattemptedAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand(OutboxSql.ReleaseUnattempted);
        cmd.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = ids.ToArray() });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> SweepExhaustedAsync(DateTimeOffset now, int maxAttempts, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand(OutboxSql.SweepExhausted);
        cmd.Parameters.AddWithValue("now", now.UtcDateTime); cmd.Parameters.AddWithValue("max_attempts", maxAttempts);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<OutboxBacklog> GetBacklogAsync(DateTimeOffset now, CancellationToken ct)
    {
        // Backlog khong ap dung loc moc/tenant o day: dung cung tham so voi Claim neu can; mac dinh dem toan bo.
        await using var cmd = _ds.CreateCommand(OutboxSql.Backlog);
        cmd.Parameters.AddWithValue("now", now.UtcDateTime);
        cmd.Parameters.AddWithValue("min_occurred_at", DateTime.UnixEpoch);
        cmd.Parameters.Add(new NpgsqlParameter("excluded_tenants", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = Array.Empty<Guid>() });
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        await rd.ReadAsync(ct);
        DateTimeOffset? oldest = rd.IsDBNull(4) ? null : new DateTimeOffset(DateTime.SpecifyKind(rd.GetDateTime(4), DateTimeKind.Utc));
        return new OutboxBacklog(rd.GetInt64(0), rd.GetInt64(1), rd.GetInt64(2), rd.GetInt64(3), oldest);
    }
}
