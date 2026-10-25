using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace AURA.Analysis.Api.Upload;

/// <summary>Short, independently committed checkpoints. No external HTTP/blob operations or automatic cleanup.</summary>
public sealed class PostgresUploadRecoveryRepository(NpgsqlDataSource dataSource) : IUploadRecoveryJournal
{
    private static async Task Lock(NpgsqlConnection c, NpgsqlTransaction tx, IdempotencyScope scope, CancellationToken ct)
    {
        if (scope.TenantId == Guid.Empty || scope.RequesterId == Guid.Empty || scope.Key == Guid.Empty)
            throw new ArgumentException("Invalid recovery scope.");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"aura-upload-recovery-v1:{scope.TenantId:D}:{scope.RequesterId:D}:{scope.Key:D}"));
        await using var cmd = new NpgsqlCommand("SELECT pg_try_advisory_xact_lock($1);", c, tx);
        cmd.Parameters.AddWithValue(BinaryPrimitives.ReadInt64BigEndian(bytes));
        if (await cmd.ExecuteScalarAsync(ct) is not true) throw UploadRecovery.InProgress();
    }

    private static async Task<UploadRecoveryRecord?> Load(NpgsqlConnection c, NpgsqlTransaction tx, IdempotencyScope scope,
        CancellationToken ct, bool forUpdate = false)
    {
        var sql = """
            SELECT a.id,a.attempt_no,a.revision,a.phase,a.plan::text,o.billing_owner,a.container,a.abort_reason,a.effects::text,
                   o.id,o.tenant_id,o.requester_id,o.idempotency_key,o.fingerprint,o.patient_id,o.owner_id,o.fingerprint_version
            FROM analysis.upload_recovery_operations o
            JOIN analysis.upload_recovery_attempts a ON a.operation_id=o.id
            WHERE o.tenant_id=$1 AND o.requester_id=$2 AND o.idempotency_key=$3
            ORDER BY a.attempt_no DESC LIMIT 1
            """ + (forUpdate ? " FOR UPDATE OF a;" : ";");
        await using var cmd = new NpgsqlCommand(sql, c, tx);
        cmd.Parameters.AddWithValue(scope.TenantId); cmd.Parameters.AddWithValue(scope.RequesterId); cmd.Parameters.AddWithValue(scope.Key);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        try
        {
            var plan = JsonSerializer.Deserialize<BatchPlan>(reader.GetString(4), UploadRecovery.Json)!;
            var effects = JsonSerializer.Deserialize<UploadRecoveryEffect[]>(reader.GetString(8), UploadRecovery.Json)!;
            var phase = ParsePhase(reader.GetString(3));
            var r = new UploadRecoveryRecord(reader.GetGuid(0), reader.GetInt32(1), reader.GetInt64(2), phase,
                plan, reader.GetString(5), reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7), effects);
            UploadRecovery.Validate(r);
            if (plan.BatchId != reader.GetGuid(9) || plan.TenantId != reader.GetGuid(10) || plan.RequesterId != reader.GetGuid(11)
                || plan.IdempotencyKey != reader.GetGuid(12) || plan.Fingerprint != reader.GetString(13)
                || plan.PatientId != reader.GetGuid(14) || plan.OwnerId != reader.GetGuid(15) || plan.FingerprintVersion != reader.GetString(16))
                throw new ArgumentException("Recovery binding mismatch.");
            return r;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException or NullReferenceException)
        { throw UploadFailure.Dependency("Analysis recovery journal"); }
    }

    private static UploadRecoveryPhase ParsePhase(string phase) => phase switch
    {
        "PENDING" => UploadRecoveryPhase.Pending, "ABORTING" => UploadRecoveryPhase.Aborting,
        "ABORTED" => UploadRecoveryPhase.Aborted, "ACCEPTED" => UploadRecoveryPhase.Accepted,
        _ => throw new ArgumentException("Invalid recovery phase.")
    };
    private static string Phase(UploadRecoveryPhase phase) => phase.ToString().ToUpperInvariant();

    public async Task<UploadRecoveryRecord?> ReadAsync(IdempotencyScope scope, CancellationToken ct)
    {
        await using var c = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        var r = await Load(c, tx, scope, ct);
        await tx.CommitAsync(ct); return r;
    }

    public async Task<UploadRecoveryRecord> PrepareAsync(BatchPlan plan, string billingOwner, string container, CancellationToken ct)
    {
        var scope = UploadRecovery.Scope(plan);
        await using var c = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        await Lock(c, tx, scope, ct);
        var previous = await Load(c, tx, scope, ct, true);
        if (previous is not null)
        {
            if (previous.Plan.Fingerprint != plan.Fingerprint)
                throw new UploadFailure(409, "ERR_IDEMPOTENCY_KEY_REUSED", "Idempotency-Key đã dùng với nội dung khác.");
            if (previous.Plan.PatientId != plan.PatientId || previous.Plan.OwnerId != plan.OwnerId
                || previous.Plan.BatchId != plan.BatchId || previous.BillingOwner != billingOwner || previous.Container != container)
                throw UploadFailure.Dependency("Profile/recovery binding");
            if (previous.Phase != UploadRecoveryPhase.Aborted)
            {
                // Called while the coordinator owns the upload session. Fence any older owner
                // whose upload DB connection died while an external request was in flight.
                var claimed = previous.Phase is UploadRecoveryPhase.Pending or UploadRecoveryPhase.Aborting
                    ? previous with { Revision = checked(previous.Revision + 1) } : previous;
                if (claimed != previous) await Save(c,tx,previous,claimed,ct);
                await tx.CommitAsync(ct); return claimed;
            }
        }
        if (previous is null)
        {
            await using var accepted = new NpgsqlCommand("SELECT request_fingerprint FROM analysis.analysis_requests WHERE tenant_id=$1 AND requester_id=$2 AND idempotency_key=$3;", c, tx);
            accepted.Parameters.AddWithValue(scope.TenantId); accepted.Parameters.AddWithValue(scope.RequesterId); accepted.Parameters.AddWithValue(scope.Key);
            if (await accepted.ExecuteScalarAsync(ct) is string acceptedFingerprint)
            {
                if (acceptedFingerprint != plan.Fingerprint) throw new UploadFailure(409,"ERR_IDEMPOTENCY_KEY_REUSED","Idempotency-Key đã dùng với nội dung khác.");
                throw UploadRecovery.InProgress(); // The normal accepted-response replay path must handle this request.
            }
        }
        var prepared = UploadRecovery.Prepare(plan, billingOwner, container, previous);
        if (previous is null)
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO analysis.upload_recovery_operations
                  (id,tenant_id,requester_id,idempotency_key,fingerprint,fingerprint_version,patient_id,owner_id,billing_owner)
                VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9);
                """, c, tx);
            foreach (var value in new object[] { plan.BatchId, plan.TenantId, plan.RequesterId, plan.IdempotencyKey,
                plan.Fingerprint, plan.FingerprintVersion, plan.PatientId, plan.OwnerId, billingOwner }) insert.Parameters.AddWithValue(value);
            await insert.ExecuteNonQueryAsync(ct);
        }
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO analysis.upload_recovery_attempts(id,operation_id,attempt_no,revision,phase,container,plan,effects)
            VALUES ($1,$2,$3,1,'PENDING',$4,$5,$6);
            """, c, tx))
        {
            insert.Parameters.AddWithValue(prepared.AttemptId); insert.Parameters.AddWithValue(plan.BatchId);
            insert.Parameters.AddWithValue(prepared.AttemptNo); insert.Parameters.AddWithValue(container);
            insert.Parameters.AddWithValue(NpgsqlDbType.Jsonb, JsonSerializer.Serialize(prepared.Plan, UploadRecovery.Json));
            insert.Parameters.AddWithValue(NpgsqlDbType.Jsonb, JsonSerializer.Serialize(prepared.Effects, UploadRecovery.Json));
            await insert.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct); return prepared;
    }

    public async Task<UploadRecoveryRecord> RecordAsync(IdempotencyScope scope, Guid attemptId, long expectedRevision,
        UploadRecoveryEvent action, Guid analysisId = default, Guid? reservationId = null, string? reason = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await using var c = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        await Lock(c, tx, scope, ct);
        var r = await Load(c, tx, scope, ct, true) ?? throw UploadRecovery.InProgress();
        if (r.AttemptId != attemptId || r.Revision != expectedRevision) throw UploadRecovery.InProgress();
        if (action == UploadRecoveryEvent.BeginAbort && r.Phase == UploadRecoveryPhase.Pending)
        {
            // A request accepted by an older writer is also authoritative. Never infer abort from a missing ack.
            await using var accepted = new NpgsqlCommand("""
                SELECT id,request_fingerprint FROM analysis.analysis_requests
                WHERE tenant_id=$1 AND requester_id=$2 AND idempotency_key=$3;
                """, c, tx);
            accepted.Parameters.AddWithValue(scope.TenantId); accepted.Parameters.AddWithValue(scope.RequesterId); accepted.Parameters.AddWithValue(scope.Key);
            await using var reader = await accepted.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct)) throw UploadRecovery.InProgress();
        }
        var changed = UploadRecovery.Apply(r, action, analysisId, reservationId, reason);
        if (changed != r) await Save(c, tx, r, changed, ct);
        await tx.CommitAsync(ct); return changed;
    }

    private static async Task Save(NpgsqlConnection c, NpgsqlTransaction tx, UploadRecoveryRecord before, UploadRecoveryRecord after, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            UPDATE analysis.upload_recovery_attempts
            SET revision=$1,phase=$2,abort_reason=$3,effects=$4,updated_at=clock_timestamp()
            WHERE id=$5 AND revision=$6;
            """, c, tx);
        cmd.Parameters.AddWithValue(after.Revision); cmd.Parameters.AddWithValue(Phase(after.Phase));
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, (object?)after.AbortReason ?? DBNull.Value);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Jsonb, JsonSerializer.Serialize(after.Effects, UploadRecovery.Json));
        cmd.Parameters.AddWithValue(before.AttemptId); cmd.Parameters.AddWithValue(before.Revision);
        if (await cmd.ExecuteNonQueryAsync(ct) != 1) throw UploadRecovery.InProgress();
    }

    internal static async Task RejectUnjournaledCommitAsync(NpgsqlConnection c, NpgsqlTransaction tx, BatchPlan plan, CancellationToken ct)
    {
        await using (var probe = new NpgsqlCommand("SELECT to_regclass('analysis.upload_recovery_operations') IS NOT NULL;", c, tx))
            if (await probe.ExecuteScalarAsync(ct) is not true) return; // Compatibility with pre-migration repository fixtures.
        var scope = UploadRecovery.Scope(plan);
        await Lock(c, tx, scope, ct);
        await using var exists = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM analysis.upload_recovery_operations WHERE tenant_id=$1 AND requester_id=$2 AND idempotency_key=$3);", c, tx);
        exists.Parameters.AddWithValue(scope.TenantId); exists.Parameters.AddWithValue(scope.RequesterId); exists.Parameters.AddWithValue(scope.Key);
        if (await exists.ExecuteScalarAsync(ct) is true) throw UploadRecovery.InProgress();
    }

    /// <summary>Called only INSIDE the transaction that inserts the accepted request/image/job/outbox.</summary>
    internal static async Task FenceAcceptanceAsync(NpgsqlConnection c, NpgsqlTransaction tx, BatchPlan plan, CancellationToken ct)
    {
        var r = await Load(c, tx, UploadRecovery.Scope(plan), ct, true) ?? throw UploadRecovery.InProgress();
        // Exact prepared plan is frozen; mismatched IDs, keys, response, outbox or metadata cannot commit.
        if (r.AttemptId != plan.RecoveryAttemptId || r.Phase != UploadRecoveryPhase.Pending
            || !UploadRecovery.ReadyToAccept(r)
            || JsonSerializer.Serialize(r.Plan, UploadRecovery.Json) != JsonSerializer.Serialize(plan, UploadRecovery.Json))
            throw UploadRecovery.InProgress();
        var changed = r with { Phase = UploadRecoveryPhase.Accepted, Revision = checked(r.Revision + 1) };
        await Save(c, tx, r, changed, ct);
    }

    public async Task<IReadOnlyList<UploadRecoveryCandidate>> FindDueAsync(int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        await using var cmd = dataSource.CreateCommand("""
            SELECT o.tenant_id,o.requester_id,o.idempotency_key,a.id
            FROM analysis.upload_recovery_attempts a JOIN analysis.upload_recovery_operations o ON o.id=a.operation_id
            WHERE a.phase IN ('PENDING','ABORTING') AND a.updated_at <= now()-interval '15 minutes'
            ORDER BY a.updated_at,a.id LIMIT $1;
            """);
        cmd.Parameters.AddWithValue(limit);
        var result = new List<UploadRecoveryCandidate>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(new(new(reader.GetGuid(0),reader.GetGuid(1),reader.GetGuid(2)),reader.GetGuid(3)));
        return result;
    }
}
