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
    private const string SchemaVersion = "0.6";
    private static readonly string[] EyeSides = { "left", "right", "unknown" };

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

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex CorrelationPattern();

    public async Task<UploadResult> CreateAsync(UploadCommand cmd, CancellationToken ct)
    {
        var (patientId, modality, eyeSide) = ValidateForm(cmd);
        var correlationId = cmd.CorrelationId is { } c && CorrelationPattern().IsMatch(c)
            ? c : Guid.NewGuid().ToString();

        var inspections = await InspectAllAsync(cmd.Files, ct);

        var decision = await Call("Profile", () => _access.ResolveAsync(
            new PatientAccessRequest(cmd.RequesterId, cmd.ClinicId, patientId, "ANALYSIS_CREATE"), ct));
        if (!decision.Allowed)
            throw new UploadFailure(404, "ERR_NOT_FOUND", "Không tìm thấy bệnh nhân.");   // ngoài phạm vi = không tồn tại

        var fingerprint = RequestFingerprint.Compute(patientId, modality, eyeSide, inspections.Select(i => i.Sha256));
        var scope = new IdempotencyScope(decision.TenantId, cmd.RequesterId, cmd.IdempotencyKey);

        await using var session = await Call("Analysis database", () => _repository.BeginAsync(scope, fingerprint, ct));
        if (session.State == IdempotencyState.Conflict)
            throw new UploadFailure(409, "ERR_IDEMPOTENCY_KEY_REUSED", "Idempotency-Key đã dùng với nội dung khác.");
        if (session.State == IdempotencyState.Replay && session.StoredResponse is { } stored)
            return new UploadResult(stored, true);

        var now = _clock.GetUtcNow();
        var plan = await BuildPlanAsync(cmd, decision, patientId, modality, eyeSide, correlationId, fingerprint,
            inspections, session, now, ct);

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
                            cmd.IdempotencyKey.ToString(), img.AnalysisId.ToString())), ct));
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
            await ReleaseAsync(decision.TenantId, reserved, cmd, ct);
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

    // ---- Kiểm tra form ----
    private (Guid patientId, string modality, string eyeSide) ValidateForm(UploadCommand cmd)
    {
        var errors = new List<ErrorDetail>();

        Guid patientId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(cmd.PatientId)) errors.Add(new("patientId", "required"));
        else if (!Guid.TryParse(cmd.PatientId, out patientId) || patientId == Guid.Empty) errors.Add(new("patientId", "invalid_uuid"));

        var modality = string.IsNullOrWhiteSpace(cmd.Modality) ? "FUNDUS" : cmd.Modality;
        if (modality != "FUNDUS") errors.Add(new("modality", "unsupported"));   // OCT hoặc khác: 400 trước mọi bước còn lại

        var eyeSide = string.IsNullOrWhiteSpace(cmd.EyeSide) ? "unknown" : cmd.EyeSide;
        if (!EyeSides.Contains(eyeSide)) errors.Add(new("eyeSide", "invalid"));

        if (cmd.Files.Count == 0) errors.Add(new("images", "required"));
        else if (cmd.Files.Count > _options.MaxImagesPerRequest) errors.Add(new("images", "too_many"));

        if (errors.Count > 0) throw UploadFailure.Validation(errors.ToArray());

        long total = cmd.Files.Sum(f => f.Length);
        if (total > _options.MaxRequestBytes)
            throw new UploadFailure(413, "ERR_PAYLOAD_TOO_LARGE", "Request vượt giới hạn kích thước.",
                false, new[] { new ErrorDetail("images", "request_too_large") });

        return (patientId, modality, eyeSide);
    }

    // ---- Kiểm tra từng ảnh: lỗi bất kỳ thì từ chối cả request trước khi reserve ----
    private async Task<List<ImageInspection>> InspectAllAsync(IReadOnlyList<IUploadedFile> files, CancellationToken ct)
    {
        var ok = new List<ImageInspection>(files.Count);
        var problems = new List<(int Index, ImageProblem Problem)>();
        for (int i = 0; i < files.Count; i++)
        {
            await using var s = files[i].OpenReadStream();
            var outcome = await ImageInspector.InspectAsync(s, files[i].ContentType, _options, ct);
            if (outcome.Problem is { } p) problems.Add((i, p));
            else ok.Add(outcome.Image!);
        }
        if (problems.Count > 0)
        {
            var first = problems[0].Problem;
            throw new UploadFailure(first.Status, first.Code, MessageFor(first),
                false, problems.Select(p => new ErrorDetail($"images[{p.Index}]", p.Problem.Issue)).ToArray());
        }
        return ok;
    }

    private static string MessageFor(ImageProblem p) => p.Status switch
    {
        413 => "File vượt giới hạn kích thước theo policy.",
        415 => "Chỉ nhận ảnh JPEG hoặc PNG.",
        _ => "Dữ liệu không hợp lệ."
    };

    // ---- Dựng kế hoạch ghi: ID xác định theo (tenant, requester, key) nên retry tạo lại đúng ID cũ ----
    private async Task<BatchPlan> BuildPlanAsync(UploadCommand cmd, AccessDecision d, Guid patientId, string modality,
        string eyeSide, string correlationId, string fingerprint, List<ImageInspection> inspections,
        IUploadSession session, DateTimeOffset now, CancellationToken ct)
    {
        string baseKey = string.Join("|", d.TenantId, cmd.RequesterId, cmd.IdempotencyKey);
        var batchId = DeterministicIds.Create("batch", baseKey);
        var images = new List<ImagePlan>();
        var outbox = new List<OutboxPlan>();
        var items = new List<AcceptedItem>();
        var firstInRequest = new Dictionary<string, Guid>();

        for (int i = 0; i < inspections.Count; i++)
        {
            var insp = inspections[i];
            var analysisId = DeterministicIds.Create("analysis", baseKey, i.ToString());
            var fileId = DeterministicIds.Create("file", baseKey, i.ToString());
            var objectKey = $"{d.TenantId:D}/{batchId:D}/{analysisId:D}";   // chỉ UUID; không có tên file/tên bệnh nhân

            Guid? existing = firstInRequest.TryGetValue(insp.Sha256, out var sameBatch)
                ? sameBatch
                : await Call("Analysis database", () => session.FindExistingAnalysisAsync(patientId, modality, eyeSide, insp.Sha256, ct));

            string fileName = SafeFileName(cmd.Files[i].FileName);

            if (existing is { } existingId)
            {
                images.Add(new ImagePlan(analysisId, fileId, objectKey, fileName, insp.MimeType, insp.SizeBytes,
                    insp.Width, insp.Height, insp.Sha256, modality, eyeSide, "DUPLICATE", existingId, null));
                items.Add(new AcceptedItem(analysisId, fileName, "DUPLICATE",
                    new ItemError("ERR_DUPLICATE_REQUEST", "Ảnh đã được gửi trước đó.", false)));
                continue;
            }

            firstInRequest[insp.Sha256] = analysisId;
            var jobId = DeterministicIds.Create("job", analysisId.ToString(), "1");
            var messageId = DeterministicIds.Create("job-message", jobId.ToString());
            images.Add(new ImagePlan(analysisId, fileId, objectKey, fileName, insp.MimeType, insp.SizeBytes,
                insp.Width, insp.Height, insp.Sha256, modality, eyeSide, "QUEUED", null,
                new JobPlan(jobId, messageId, correlationId)));
            items.Add(new AcceptedItem(analysisId, fileName, "QUEUED", null));

            string occurred = now.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
            var requestedEventId = DeterministicIds.Create("event-requested", analysisId.ToString());
            outbox.Add(new OutboxPlan(requestedEventId, analysisId, "analysis.requested", SchemaVersion,
                JsonSerializer.Serialize(new
                {
                    eventId = requestedEventId,
                    eventType = "analysis.requested",
                    schemaVersion = SchemaVersion,
                    occurredAt = occurred,
                    tenantId = d.TenantId,
                    correlationId,
                    analysisId,
                    data = new { patientId, batchId }
                }, UploadJson.Options), now));
            outbox.Add(new OutboxPlan(messageId, analysisId, "analysis.job.run", SchemaVersion,
                JsonSerializer.Serialize(new
                {
                    messageId,
                    messageType = "analysis.job.run",
                    schemaVersion = SchemaVersion,
                    occurredAt = occurred,
                    tenantId = d.TenantId,
                    correlationId,
                    jobId,
                    analysisId,
                    batchId,
                    imageFileId = fileId,
                    attempt = 1
                }, UploadJson.Options), now));
        }

        var response = new AcceptedResponse(batchId, items, now.UtcDateTime, $"/api/v1/batches/{batchId:D}");
        return new BatchPlan(batchId, d.TenantId, d.OwnerPrincipalId, cmd.RequesterId, patientId, cmd.IdempotencyKey,
            fingerprint, RequestFingerprint.Version, correlationId, now, response, images, outbox);
    }

    private static string SafeFileName(string name)
    {
        var n = Path.GetFileName(name ?? "");
        if (string.IsNullOrWhiteSpace(n)) n = "image";
        return n.Length > 255 ? n[..255] : n;
    }

    // ---- Compensation / bọc lỗi phụ thuộc ----
    private async Task ReleaseAsync(Guid tenantId, List<(Guid AnalysisId, Guid ReservationId)> reserved,
        UploadCommand cmd, CancellationToken ct)
    {
        foreach (var (analysisId, reservationId) in reserved)
        {
            try
            {
                await _credits.ReleaseAsync(new ReleaseCredit(tenantId, reservationId,
                    DeterministicIds.Create("release", tenantId.ToString(), cmd.RequesterId.ToString(),
                        cmd.IdempotencyKey.ToString(), analysisId.ToString())), ct);
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
        foreach (var key in keys)
        {
            try { await _storage.DeleteAsync(_options.OriginalsContainer, key, CancellationToken.None); }
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
