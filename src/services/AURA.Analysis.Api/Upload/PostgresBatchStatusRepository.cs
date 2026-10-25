using System.Text.Json;
using Npgsql;

namespace AURA.Analysis.Api.Upload;

public sealed class PostgresBatchStatusRepository(NpgsqlDataSource dataSource) : IBatchStatusRepository
{
    public async Task<BatchIdentity?> FindIdentityAsync(Guid batchId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (batchId == Guid.Empty) return null;
        await using var command = dataSource.CreateCommand("""
            SELECT id,tenant_id,patient_id,owner_id FROM analysis.analysis_requests WHERE id=$1;
            """);
        command.Parameters.AddWithValue(batchId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3)) : null;
    }

    public async Task<BatchStatusSnapshot?> ReadAsync(BatchIdentity identity, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (identity.BatchId == Guid.Empty || identity.TenantId == Guid.Empty
            || identity.PatientId == Guid.Empty || identity.OwnerId == Guid.Empty) return null;
        // One statement gives a consistent snapshot of images + latest attempts/results during polling.
        // Never SELECT object_key, full risk JSON, annotation paths, job error_details or outbox payloads.
        await using var command = dataSource.CreateCommand("""
            SELECT r.created_at,r.accepted_response::text,i.id,i.processing_status,i.review_status,
                   j.id,j.attempt_no,j.status,j.retryable,j.error_code,
                   CASE WHEN i.processing_status='COMPLETED' THEN ar.risk_level ELSE NULL END,i.existing_analysis_id
            FROM analysis.analysis_requests r
            LEFT JOIN analysis.images i ON i.tenant_id=r.tenant_id AND i.analysis_request_id=r.id
            LEFT JOIN LATERAL (
                SELECT x.id,x.attempt_no,x.status,x.retryable,x.error_code
                FROM analysis.jobs x WHERE x.tenant_id=i.tenant_id AND x.image_id=i.id
                ORDER BY x.attempt_no DESC LIMIT 1
            ) j ON true
            LEFT JOIN LATERAL (
                SELECT x.risk_assessment->>'riskLevel' AS risk_level
                FROM analysis.analysis_results x
                JOIN analysis.jobs xj ON xj.tenant_id=x.tenant_id AND xj.id=x.job_id AND xj.image_id=x.image_id
                WHERE x.tenant_id=i.tenant_id AND x.image_id=i.id
                ORDER BY x.version DESC LIMIT 1
            ) ar ON true
            WHERE r.id=$1 AND r.tenant_id=$2 AND r.patient_id=$3 AND r.owner_id=$4
            ORDER BY i.id LIMIT 101;
            """);
        foreach (var value in new[] { identity.BatchId, identity.TenantId, identity.PatientId, identity.OwnerId })
            command.Parameters.AddWithValue(value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var created = reader.GetDateTime(0);
        AcceptedResponse? accepted;
        try { accepted = JsonSerializer.Deserialize<AcceptedResponse>(reader.GetString(1), UploadJson.Options); }
        catch (JsonException) { throw UploadFailure.Dependency("Analysis database"); }
        if (accepted is null || accepted.BatchId != identity.BatchId || accepted.Items is null
            || accepted.Items.Count is < 1 or > 100 || accepted.Items.Any(x => x is null || x.AnalysisId == Guid.Empty)
            || accepted.Items.Select(x => x.AnalysisId).Distinct().Count() != accepted.Items.Count)
            throw UploadFailure.Dependency("Analysis database");
        var original = accepted.Items.ToDictionary(x => x.AnalysisId);
        var rows = new Dictionary<Guid, BatchStatusRow>();
        do
        {
            if (reader.IsDBNull(2)) throw UploadFailure.Dependency("Analysis database");
            var id = reader.GetGuid(2);
            if (!original.TryGetValue(id, out var item) || rows.ContainsKey(id)) throw UploadFailure.Dependency("Analysis database");
            BatchJobStatus? job = null;
            if (!reader.IsDBNull(5))
            {
                var attempt = reader.GetInt32(6);
                job = new(reader.GetGuid(5), reader.GetString(7), attempt - 1, attempt);
            }
            rows.Add(id, new(id, item.FileName, reader.GetString(3), reader.GetString(4), job,
                !reader.IsDBNull(8) && reader.GetBoolean(8), reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetGuid(11)));
            if (rows.Count > 100) throw UploadFailure.Dependency("Analysis database");
        } while (await reader.ReadAsync(ct));
        if (rows.Count != accepted.Items.Count) throw UploadFailure.Dependency("Analysis database");
        return new(identity.BatchId, created, accepted.Items.Select(i => rows[i.AnalysisId]).ToArray());
    }
}
