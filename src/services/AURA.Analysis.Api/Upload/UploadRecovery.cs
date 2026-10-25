using System.Text.Json;
using System.Text.Json.Serialization;

namespace AURA.Analysis.Api.Upload;

public enum UploadRecoveryPhase { Pending, Aborting, Aborted, Accepted }
public enum UploadRecoveryEvent { ReserveIntent, Reserved, CreditDenied, BlobIntent, BlobStored, Released, BlobDeleted, BeginAbort, FinishAbort }

/// <summary>Internal write-ahead state, never an HTTP response. An intent precedes its external request.</summary>
public sealed record UploadRecoveryEffect(Guid AnalysisId, int Generation, Guid ReserveKey, Guid ReleaseKey,
    bool ReserveStarted = false, Guid? ReservationId = null, bool CreditDenied = false,
    bool ReleaseConfirmed = false, bool BlobStarted = false, bool BlobConfirmed = false, bool DeleteConfirmed = false);
public sealed record UploadRecoveryRecord(Guid AttemptId, int AttemptNo, long Revision, UploadRecoveryPhase Phase,
    BatchPlan Plan, string BillingOwner, string Container, string? AbortReason, IReadOnlyList<UploadRecoveryEffect> Effects);
public sealed record UploadRecoveryCandidate(IdempotencyScope Scope, Guid AttemptId);

public static class UploadRecovery
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public static UploadRecoveryRecord Prepare(BatchPlan plan, string billingOwner, string container,
        UploadRecoveryRecord? previous = null)
    {
        if (previous is not null)
        {
            Validate(previous);
            if (previous.Phase != UploadRecoveryPhase.Aborted) throw InProgress();
            if (Scope(previous.Plan) != Scope(plan) || previous.Plan.BatchId != plan.BatchId
                || previous.Plan.Fingerprint != plan.Fingerprint || previous.Plan.PatientId != plan.PatientId
                || previous.Plan.OwnerId != plan.OwnerId || previous.BillingOwner != billingOwner || previous.Container != container)
                throw new ArgumentException("Recovery binding cannot change.");
            if (!previous.Plan.Images.Select(i => (i.AnalysisId, i.FileId, i.Sha256, i.Modality, i.EyeSide))
                .SequenceEqual(plan.Images.Select(i => (i.AnalysisId, i.FileId, i.Sha256, i.Modality, i.EyeSide))))
                throw new ArgumentException("Retry image identities cannot change.");
        }
        var attempt = Guid.NewGuid();
        var images = plan.Images.Select(i => i with { ObjectKey = $"{plan.TenantId:D}/{plan.BatchId:D}/{i.AnalysisId:D}/{attempt:D}" }).ToArray();
        var prepared = plan with { RecoveryAttemptId = attempt, Images = images };
        var effects = images.Select(image =>
        {
            var old = previous?.Effects.Single(e => e.AnalysisId == image.AnalysisId);
            var generation = checked((old?.Generation ?? 1) + (old?.ReleaseConfirmed == true ? 1 : 0));
            return new UploadRecoveryEffect(image.AnalysisId, generation,
                image.Job is null ? Guid.Empty : ReserveKey(prepared, image.AnalysisId, generation),
                image.Job is null ? Guid.Empty : DeterministicIds.Create("recovery-release-v1", attempt.ToString("D"), image.AnalysisId.ToString("D")));
        }).ToArray();
        var record = new UploadRecoveryRecord(attempt, checked((previous?.AttemptNo ?? 0) + 1), 1,
            UploadRecoveryPhase.Pending, prepared, billingOwner, container, null, effects);
        Validate(record);
        return record;
    }

    public static IdempotencyScope Scope(BatchPlan plan) => new(plan.TenantId, plan.RequesterId, plan.IdempotencyKey);
    public static Guid ReserveKey(BatchPlan plan, Guid analysis, int generation) => DeterministicIds.Create("recovery-reserve-v1",
        plan.TenantId.ToString("D"), plan.RequesterId.ToString("D"), plan.IdempotencyKey.ToString("D"), analysis.ToString("D"), generation.ToString(System.Globalization.CultureInfo.InvariantCulture));
    public static UploadFailure InProgress() => new(409, "ERR_REQUEST_IN_PROGRESS", "Upload đang chờ xử lý hoặc đối soát. Gửi lại cùng key.", true);

    public static void Validate(UploadRecoveryRecord r)
    {
        var p = r.Plan;
        if (r.AttemptId == Guid.Empty || r.AttemptNo < 1 || r.Revision < 1 || !Enum.IsDefined(r.Phase)
            || p is null || p.RecoveryAttemptId != r.AttemptId || p.BatchId == Guid.Empty || p.TenantId == Guid.Empty
            || p.RequesterId == Guid.Empty || p.OwnerId == Guid.Empty || p.PatientId == Guid.Empty || p.IdempotencyKey == Guid.Empty
            || p.FingerprintVersion != RequestFingerprint.Version || !Hash(p.Fingerprint)
            || p.CreatedAt == default || p.Response is null || p.Response.BatchId != p.BatchId
            || p.Response.CreatedAt == default || p.Response.StatusUrl != $"/api/v1/batches/{p.BatchId:D}" || p.Images is null || p.Images.Count is < 1 or > 100
            || p.Response.Items is null || p.Response.Items.Count != p.Images.Count || p.Outbox is null
            || p.Outbox.Count != 2 * p.Images.Count(i => i.Job is not null)
            || r.Effects is null || r.Effects.Count != p.Images.Count || r.Container != "aura-originals")
            throw new ArgumentException("Malformed recovery record.");
        var owner = (r.BillingOwner ?? "").Split(':');
        if (owner.Length != 2 || owner[0] is not ("ACCOUNT" or "CLINIC") || !Guid.TryParse(owner[1], out var ownerId)
            || ownerId == Guid.Empty || (owner[0] == "ACCOUNT" && ownerId != p.OwnerId)) throw new ArgumentException("Invalid recovery billing owner.");
        if (r.Phase is UploadRecoveryPhase.Aborting or UploadRecoveryPhase.Aborted
            ? r.AbortReason is not ("ANALYSIS_ABORT" or "STORAGE_ABORT" or "VALIDATION_ABORT")
            : r.AbortReason is not null)
            throw new ArgumentException("Invalid recovery abort reason.");
        if (p.Images.Select(i => i.AnalysisId).Distinct().Count() != p.Images.Count
            || p.Images.Select(i => i.FileId).Distinct().Count() != p.Images.Count) throw new ArgumentException("Duplicate recovery image identities.");
        for (var index = 0; index < p.Images.Count; index++)
        {
            var image = p.Images[index]; var e = r.Effects[index];
            var charge = image.Job is not null;
            if (image.AnalysisId == Guid.Empty || image.FileId == Guid.Empty || !Hash(image.Sha256)
                || image.AnalysisId != p.Response.Items[index].AnalysisId || image.AnalysisId != e.AnalysisId
                || image.ObjectKey != $"{p.TenantId:D}/{p.BatchId:D}/{image.AnalysisId:D}/{r.AttemptId:D}"
                || image.Modality != "FUNDUS" || image.EyeSide is not ("left" or "right" or "unknown")
                || image.MimeType is not ("image/jpeg" or "image/png") || image.Width < 1 || image.Height < 1 || image.SizeBytes < 1
                || image.ProcessingStatus != (charge ? "QUEUED" : "DUPLICATE")
                || (image.ExistingAnalysisId is null) != charge || e.Generation < 1
                || e.ReserveKey != (charge ? ReserveKey(p, image.AnalysisId, e.Generation) : Guid.Empty)
                || e.ReleaseKey != (charge ? DeterministicIds.Create("recovery-release-v1", r.AttemptId.ToString("D"), image.AnalysisId.ToString("D")) : Guid.Empty)
                || ((!charge || !e.ReserveStarted) && (e.ReservationId is not null || e.CreditDenied || e.ReleaseConfirmed))
                || (!charge && e.ReserveStarted) || e.ReservationId == Guid.Empty
                || (e.ReservationId is not null && e.CreditDenied) || (e.ReleaseConfirmed && e.ReservationId is null)
                || (e.BlobConfirmed && !e.BlobStarted) || (e.DeleteConfirmed && !e.BlobStarted)
                || (r.Phase == UploadRecoveryPhase.Pending && (e.ReleaseConfirmed || e.DeleteConfirmed)))
                throw new ArgumentException("Malformed recovery image/effect state.");
        }
        if (r.Phase == UploadRecoveryPhase.Accepted && !ReadyToAccept(r)) throw new ArgumentException("Accepted upload is not complete.");
        if (r.Phase == UploadRecoveryPhase.Aborted && !ReadyToFinishAbort(r)) throw new ArgumentException("Abort has unresolved effects.");
    }

    private static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static bool ReadyToAccept(UploadRecoveryRecord r) => r.Effects.All(e => e.BlobConfirmed && !e.DeleteConfirmed
        && (e.ReserveKey == Guid.Empty || (e.ReservationId is not null && !e.CreditDenied && !e.ReleaseConfirmed)));
    public static bool ReadyToFinishAbort(UploadRecoveryRecord r) => r.Effects.All(e =>
        (!e.ReserveStarted || e.CreditDenied || e.ReleaseConfirmed) && (!e.BlobStarted || e.DeleteConfirmed));

    public static UploadRecoveryRecord Apply(UploadRecoveryRecord r, UploadRecoveryEvent action, Guid analysisId = default,
        Guid? reservationId = null, string? reason = null)
    {
        Validate(r);
        if (!Enum.IsDefined(action)) throw new ArgumentException("Unknown recovery event.");
        if (action == UploadRecoveryEvent.BeginAbort)
        {
            if (r.Phase == UploadRecoveryPhase.Accepted) return r; // Acceptance is authoritative; never authorize cleanup.
            if (r.Phase is UploadRecoveryPhase.Aborting or UploadRecoveryPhase.Aborted) return r;
            if (reason is not ("ANALYSIS_ABORT" or "STORAGE_ABORT" or "VALIDATION_ABORT")) throw new ArgumentException("Invalid abort reason.");
            return r with { Phase = UploadRecoveryPhase.Aborting, AbortReason = reason, Revision = checked(r.Revision + 1) };
        }
        if (action == UploadRecoveryEvent.FinishAbort)
        {
            if (r.Phase == UploadRecoveryPhase.Aborted) return r;
            if (r.Phase != UploadRecoveryPhase.Aborting || !ReadyToFinishAbort(r)) throw InProgress();
            return r with { Phase = UploadRecoveryPhase.Aborted, Revision = checked(r.Revision + 1) };
        }
        if (r.Phase is UploadRecoveryPhase.Accepted or UploadRecoveryPhase.Aborted) throw InProgress();
        var index = r.Effects.ToList().FindIndex(e => e.AnalysisId == analysisId);
        if (index < 0) throw new ArgumentException("Unknown recovery analysis.");
        var old = r.Effects[index]; var next = old;
        switch (action)
        {
            case UploadRecoveryEvent.ReserveIntent:
                if (r.Phase != UploadRecoveryPhase.Pending || old.ReserveKey == Guid.Empty || old.CreditDenied) throw InProgress();
                next = old with { ReserveStarted = true }; break;
            case UploadRecoveryEvent.Reserved:
                if (!old.ReserveStarted || old.CreditDenied || reservationId is null || reservationId == Guid.Empty
                    || (old.ReservationId is not null && old.ReservationId != reservationId)) throw InProgress();
                next = old with { ReservationId = reservationId }; break;
            case UploadRecoveryEvent.CreditDenied:
                if (!old.ReserveStarted || old.ReservationId is not null) throw InProgress();
                next = old with { CreditDenied = true }; break;
            case UploadRecoveryEvent.BlobIntent:
                if (r.Phase != UploadRecoveryPhase.Pending || (old.ReserveKey != Guid.Empty && old.ReservationId is null)) throw InProgress();
                next = old with { BlobStarted = true }; break;
            case UploadRecoveryEvent.BlobStored:
                if (!old.BlobStarted) throw InProgress(); next = old with { BlobConfirmed = true }; break;
            case UploadRecoveryEvent.Released:
                if (r.Phase != UploadRecoveryPhase.Aborting || old.ReservationId is null) throw InProgress();
                next = old with { ReleaseConfirmed = true }; break;
            case UploadRecoveryEvent.BlobDeleted:
                if (r.Phase != UploadRecoveryPhase.Aborting || !old.BlobStarted) throw InProgress();
                next = old with { DeleteConfirmed = true }; break;
        }
        if (next == old) return r;
        var effects = r.Effects.ToArray(); effects[index] = next;
        var changed = r with { Effects = effects, Revision = checked(r.Revision + 1) };
        Validate(changed); return changed;
    }
}

public interface IUploadRecoveryJournal
{
    Task<UploadRecoveryRecord?> ReadAsync(IdempotencyScope scope,CancellationToken ct);
    Task<UploadRecoveryRecord> PrepareAsync(BatchPlan plan,string billingOwner,string container,CancellationToken ct);
    Task<UploadRecoveryRecord> RecordAsync(IdempotencyScope scope,Guid attemptId,long expectedRevision,
        UploadRecoveryEvent action,Guid analysisId=default,Guid? reservationId=null,string? reason=null,CancellationToken ct=default);
    Task<IReadOnlyList<UploadRecoveryCandidate>> FindDueAsync(int limit,CancellationToken ct);
}
