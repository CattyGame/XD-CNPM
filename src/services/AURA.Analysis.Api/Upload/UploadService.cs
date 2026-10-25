using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace AURA.Analysis.Api.Upload;

public static class UploadJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
}

/// <summary>
/// Luồng tạo analysis (Sprint 2 contract mục 7):
/// kiểm tra form và ảnh -> kiểm quyền Patient -> khóa idempotency/replay -> dedupe ->
/// reserve credit -> lưu ảnh private -> một transaction batch/image/job/outbox/idempotency -> 202.
/// </summary>
public sealed partial class UploadService
{

    private readonly UploadOptions _options;
    private readonly IPatientAccessResolver _access;
    private readonly ICreditReservations _credits;
    private readonly IPrivateFileStorage _storage;
    private readonly IUploadRepository _repository;
    private readonly TimeProvider _clock;
    private readonly ILogger<UploadService> _log;

    public UploadService(UploadOptions options, IPatientAccessResolver access, ICreditReservations credits,
        IPrivateFileStorage storage, IUploadRepository repository, TimeProvider clock, ILogger<UploadService> log)
    {
        _options = options; _access = access; _credits = credits; _storage = storage;
        _repository = repository; _clock = clock; _log = log;
    }


    public async Task<UploadResult> CreateAsync(UploadCommand cmd, CancellationToken ct)
    {
        await using var prepared = await new UploadPlanningService(_options,_access,_repository,_clock).PrepareAsync(cmd,ct);
        if (prepared.StoredResponse is { } replay) return new(replay,true);
        var plan = prepared.Plan!;
        var session = prepared.Session;
        var decision = prepared.Decision;
        var patientId = plan.PatientId;
        var correlationId = plan.CorrelationId;

        var newImages = plan.Images.Where(i => i.Job is not null).ToList();
        var reserved = new List<(Guid AnalysisId, Guid ReservationId)>();
        var stored2 = new List<string>();

        try
        {
            foreach (var img in newImages)
            {
                var outcome = await Call("Billing", () => _credits.ReserveAsync(
                    new ReserveCredit(decision.TenantId, decision.BillingOwner, img.AnalysisId,
                        DeterministicIds.Create("reserve", decision.TenantId.ToString(), cmd.RequesterId.ToString(),
                            cmd.IdempotencyKey.ToString(), img.AnalysisId.ToString()))
                    {
                        ActorAccountId = cmd.RequesterId, PatientId = patientId,
                        Generation = 1, CorrelationId = correlationId
                    }, ct));
                if (outcome.Status == ReservationStatus.InsufficientCredit || outcome.ReservationId is null)
                {
                    await ReleaseAsync(decision.TenantId, reserved, cmd, ct);
                    throw new UploadFailure(402, "ERR_INSUFFICIENT_CREDIT", "Không đủ credit.");
                }
                reserved.Add((img.AnalysisId, outcome.ReservationId.Value));
            }
        }
        catch
        {
            // 402 đã hoàn rồi (danh sách rỗng); lỗi Billing/hủy giữa chừng thì hoàn phần đã giữ.
            await ReleaseAsync(decision.TenantId, reserved, cmd, ct);
            throw;
        }

        try
        {
            for (int i = 0; i < plan.Images.Count; i++)
            {
                var img = plan.Images[i];
                await using var s = cmd.Files[i].OpenReadStream();
                await Call("private storage", () => _storage.PutAsync(_options.OriginalsContainer, img.ObjectKey, s, img.MimeType, ct));
                stored2.Add(img.ObjectKey);
            }
        }
        catch
        {
            // Chưa commit gì: hoàn credit và dọn file đã lưu.
            await ReleaseAsync(decision.TenantId, reserved, cmd, ct, "STORAGE_ABORT");
            await DeleteFilesAsync(stored2);
            throw;
        }

        try
        {
            await Call("Analysis database", () => session.CommitAsync(plan, ct));
        }
        catch (Exception)
        {
            // Không biết transaction đã commit hay chưa: KHÔNG hoàn credit, KHÔNG xóa file.
            // Client gửi lại cùng key sẽ dùng cùng ID và ghi đè; reconciliation sau 15 phút xử lý phần còn lại.
            _log.LogError("Commit lỗi, giữ reservation và file chờ reconciliation. batchId={BatchId} correlationId={Correlation}",
                plan.BatchId, correlationId);
            throw;
        }

        return new UploadResult(plan.Response, false);
    }

    // ---- Compensation / bọc lỗi phụ thuộc ----
    private async Task ReleaseAsync(Guid tenantId, List<(Guid AnalysisId, Guid ReservationId)> reserved,
        UploadCommand cmd, CancellationToken ct, string reason = "ANALYSIS_ABORT")
    {
        // Compensation must still be attempted after the caller disconnects, with a bounded lifetime.
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        foreach (var (analysisId, reservationId) in reserved)
        {
            try
            {
                await _credits.ReleaseAsync(new ReleaseCredit(tenantId, reservationId,
                    DeterministicIds.Create("release", tenantId.ToString(), cmd.RequesterId.ToString(),
                        cmd.IdempotencyKey.ToString(), analysisId.ToString(), reason))
                    {
                        Reason = reason, CorrelationId = cmd.CorrelationId
                    }, cleanup.Token);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Không hoàn được reservation {ReservationId}; để reconciliation xử lý.", reservationId);
            }
        }
        reserved.Clear();
    }

    private async Task DeleteFilesAsync(List<string> keys)
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        foreach (var key in keys)
        {
            try { await _storage.DeleteAsync(_options.OriginalsContainer, key, cleanup.Token); }
            catch (Exception ex) { _log.LogError(ex, "Không xóa được file mồ côi {Key}; dọn theo lịch 24 giờ.", key); }
        }
    }

    private static async Task<T> Call<T>(string what, Func<Task<T>> action)
    {
        try { return await action(); }
        catch (UploadFailure) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { throw UploadFailure.Dependency(what); }
    }

    private static async Task Call(string what, Func<Task> action)
    {
        try { await action(); }
        catch (UploadFailure) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { throw UploadFailure.Dependency(what); }
    }
}
