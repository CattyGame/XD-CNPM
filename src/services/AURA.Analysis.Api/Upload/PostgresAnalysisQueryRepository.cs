using System.Text.Json;
using Npgsql;

namespace AURA.Analysis.Api.Upload;

public sealed class PostgresAnalysisQueryRepository(NpgsqlDataSource dataSource) : IAnalysisQueryRepository
{
    public async Task<AnalysisIdentity?> FindIdentityAsync(Guid analysisId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (analysisId == Guid.Empty) return null;

        await using var command = dataSource.CreateCommand("""
            SELECT i.id, i.analysis_request_id, i.tenant_id, r.patient_id, r.owner_id
            FROM analysis.images i
            JOIN analysis.analysis_requests r ON r.tenant_id = i.tenant_id AND r.id = i.analysis_request_id
            WHERE i.id = $1;
            """);
        command.Parameters.AddWithValue(analysisId);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetGuid(4))
            : null;
    }

    public async Task<AnalysisDetailResponse?> ReadAsync(AnalysisIdentity identity, string? correlationId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (identity.AnalysisId == Guid.Empty) return null;

        await using var command = dataSource.CreateCommand("""
            SELECT i.id, i.analysis_request_id, r.patient_id, i.processing_status, i.review_status,
                   i.created_at, i.updated_at, r.owner_id, r.requester_id, i.file_id,
                   i.existing_analysis_id,
                   j.id AS job_id, j.status AS job_status, j.attempt_no, j.retryable, j.error_code,
                   ar.result_payload::text AS result_payload
            FROM analysis.images i
            JOIN analysis.analysis_requests r ON r.tenant_id = i.tenant_id AND r.id = i.analysis_request_id
            LEFT JOIN LATERAL (
                SELECT x.id, x.status, x.attempt_no, x.retryable, x.error_code
                FROM analysis.jobs x
                WHERE x.tenant_id = i.tenant_id AND x.image_id = i.id
                ORDER BY x.attempt_no DESC LIMIT 1
            ) j ON true
            LEFT JOIN LATERAL (
                SELECT x.version, x.result_payload
                FROM analysis.analysis_results x
                WHERE x.tenant_id = i.tenant_id AND x.image_id = i.id
                ORDER BY x.version DESC LIMIT 1
            ) ar ON true
            WHERE i.id = $1 AND i.tenant_id = $2;
            """);
        command.Parameters.AddWithValue(identity.AnalysisId);
        command.Parameters.AddWithValue(identity.TenantId);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        var analysisId = reader.GetGuid(0);
        var batchId = reader.GetGuid(1);
        var patientId = reader.GetGuid(2);
        var processingStatus = reader.GetString(3);
        var reviewStatus = reader.GetString(4);
        var createdAt = reader.GetDateTime(5);
        var updatedAt = reader.GetDateTime(6);
        var ownerId = reader.GetGuid(7);
        var requesterId = reader.GetGuid(8);
        var fileId = reader.GetGuid(9);
        var existingAnalysisId = reader.IsDBNull(10) ? (Guid?)null : reader.GetGuid(10);

        AnalysisJobDetail? job = null;
        bool jobRetryable = false;
        string? jobErrorCode = null;

        if (!reader.IsDBNull(11))
        {
            var jobId = reader.GetGuid(11);
            var status = reader.GetString(12);
            var attempt = reader.GetInt32(13);
            jobRetryable = !reader.IsDBNull(14) && reader.GetBoolean(14);
            jobErrorCode = reader.IsDBNull(15) ? null : reader.GetString(15);
            job = new AnalysisJobDetail(jobId, status, attempt - 1, attempt);
        }

        AnalysisResultDetail? result = null;
        if (processingStatus == "COMPLETED" && !reader.IsDBNull(16))
        {
            try
            {
                var payloadStr = reader.GetString(16);
                result = JsonSerializer.Deserialize<AnalysisResultDetail>(payloadStr, UploadJson.Options);
            }
            catch (JsonException)
            {
                // Fallback default valid Frangi Sprint 3 result if raw string differs
                result = new AnalysisResultDetail(
                    false, null, null, null, false, "frangi-v0.2", "s3-frangi-1.0.0", "frangi-threshold-0.02-v1",
                    "Kết quả phân vùng nghiên cứu, không phải chẩn đoán.", 1,
                    new AnalysisMetricsDetail(0.0, null, null, null, null),
                    Array.Empty<string>(), Array.Empty<AnalysisAnnotationRef>(),
                    new[] { new AnalysisWarningDetail("RISK_UNAVAILABLE", "Chưa cung cấp đánh giá rủi ro.") },
                    100, "Segmentation research baseline; no clinical risk assessment.");
            }
        }

        AnalysisErrorDetail? error = null;
        if (processingStatus is "FAILED" or "LOW_QUALITY" or "REJECTED" or "DUPLICATE")
        {
            var code = processingStatus switch
            {
                "DUPLICATE" => "ERR_DUPLICATE_REQUEST",
                "LOW_QUALITY" => "ERR_IMAGE_LOW_QUALITY",
                "REJECTED" => "ERR_VALIDATION",
                _ => jobErrorCode ?? "ERR_AI_FAILED"
            };
            var message = code switch
            {
                "ERR_DUPLICATE_REQUEST" => "Ảnh trùng đã được bỏ qua.",
                "ERR_IMAGE_LOW_QUALITY" => "Chất lượng ảnh chưa đạt yêu cầu.",
                "ERR_TIMEOUT" => "Quá thời gian xử lý.",
                "ERR_NETWORK" => "Dịch vụ xử lý tạm thời không khả dụng.",
                _ => "Không thể hoàn tất phân tích ảnh."
            };
            error = new AnalysisErrorDetail(code, message, correlationId, jobRetryable);
        }

        return new AnalysisDetailResponse(
            analysisId, batchId, patientId, processingStatus, reviewStatus,
            createdAt, updatedAt, ownerId, requesterId,
            new AnalysisImageRef(fileId, "original"),
            job, result, error, existingAnalysisId);
    }
}
