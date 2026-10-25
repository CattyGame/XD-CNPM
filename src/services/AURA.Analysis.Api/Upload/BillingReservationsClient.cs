using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AURA.Analysis.Api.Upload;

/// <summary>Billing's wire contract is independent of the internal Analysis domain records.</summary>
public sealed class BillingReservationsClient(HttpClient http, IServiceTokenProvider tokens) : ICreditReservations
{
    public async Task<ReservationOutcome> ReserveAsync(ReserveCredit request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var owner = ParseOwner(request.BillingOwner);
        if (request.TenantId == Guid.Empty || request.AnalysisId == Guid.Empty || request.IdempotencyKey == Guid.Empty
            || request.ActorAccountId == Guid.Empty || request.PatientId == Guid.Empty || request.Generation < 1)
            throw UploadFailure.Dependency("Billing");
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/billing/reservations")
            {
                Content = JsonContent.Create(new
                {
                    request.AnalysisId, request.TenantId, request.ActorAccountId, request.PatientId,
                    billingOwner = owner, units = 1, request.Generation
                })
            };
            ServiceHttp.SetBearer(message, await tokens.GetAsync("aura-billing", "billing:reserve", ct, request.CorrelationId));
            message.Headers.Add("Idempotency-Key", request.IdempotencyKey.ToString("D"));
            ServiceHttp.AddCorrelation(message, request.CorrelationId);
            using var response = await http.SendAsync(message, ct);
            if (response.StatusCode == HttpStatusCode.PaymentRequired)
            {
                var error = await response.Content.ReadFromJsonAsync<BillingError>(cancellationToken: ct);
                if (error?.Error?.Code != "ERR_INSUFFICIENT_CREDIT") throw UploadFailure.Dependency("Billing");
                return new(ReservationStatus.InsufficientCredit, null);
            }
            if (response.StatusCode == HttpStatusCode.Conflict) await ThrowConflictAsync(response, ct);
            if (response.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.OK)) throw UploadFailure.Dependency("Billing");
            var reservation = await response.Content.ReadFromJsonAsync<ReservationReply>(cancellationToken: ct);
            if (reservation is null || reservation.ReservationId == Guid.Empty
                || reservation.AnalysisId != request.AnalysisId || reservation.Generation != request.Generation
                || reservation.Units != 1 || reservation.CreatedAt == default)
                throw UploadFailure.Dependency("Billing");
            if (reservation.State is "CAPTURED" or "RELEASED") throw NeedsReconciliation();
            if (reservation.State != "RESERVED") throw UploadFailure.Dependency("Billing");
            return new(ReservationStatus.Reserved, reservation.ReservationId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or OperationCanceledException)
        {
            // The caller must retain the same deterministic key and reconcile unknown outcomes.
            throw UploadFailure.Dependency("Billing");
        }
    }

    public async Task ReleaseAsync(ReleaseCredit request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (request.TenantId == Guid.Empty || request.ReservationId == Guid.Empty || request.IdempotencyKey == Guid.Empty
            || request.Reason is not ("VALIDATION_ABORT" or "STORAGE_ABORT" or "ANALYSIS_ABORT" or "FINAL_FAILURE" or "LOW_QUALITY"))
            throw UploadFailure.Dependency("Billing");
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post,
                $"/internal/v1/billing/reservations/{request.ReservationId:D}/release")
            {
                Content = JsonContent.Create(new { request.Reason })
            };
            ServiceHttp.SetBearer(message, await tokens.GetAsync("aura-billing", "billing:release", ct, request.CorrelationId));
            message.Headers.Add("Idempotency-Key", request.IdempotencyKey.ToString("D"));
            ServiceHttp.AddCorrelation(message, request.CorrelationId);
            using var response = await http.SendAsync(message, ct);
            if (response.StatusCode == HttpStatusCode.Conflict) await ThrowConflictAsync(response, ct);
            if (response.StatusCode != HttpStatusCode.OK) throw UploadFailure.Dependency("Billing");
            var reservation = await response.Content.ReadFromJsonAsync<ReservationReply>(cancellationToken: ct);
            if (reservation is null || reservation.ReservationId != request.ReservationId
                || reservation.State != "RELEASED" || reservation.Units != 1
                || reservation.Generation < 1 || reservation.AnalysisId == Guid.Empty || reservation.CreatedAt == default)
                throw UploadFailure.Dependency("Billing");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or OperationCanceledException)
        {
            throw UploadFailure.Dependency("Billing");
        }
    }

    private static BillingOwner ParseOwner(string value)
    {
        var parts = (value ?? "").Split(':');
        if (parts.Length != 2 || parts[0] is not ("ACCOUNT" or "CLINIC")
            || !Guid.TryParse(parts[1], out var id) || id == Guid.Empty)
            throw UploadFailure.Dependency("Profile");
        return new(parts[0], id);
    }

    private static async Task ThrowConflictAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var error = await response.Content.ReadFromJsonAsync<BillingError>(cancellationToken: ct);
        if (error?.Error?.Code == "ERR_IDEMPOTENCY_CONFLICT")
            throw new UploadFailure(409, "ERR_IDEMPOTENCY_KEY_REUSED", "Yêu cầu Billing không khớp với idempotency key đã lưu.");
        if (error?.Error?.Code == "ERR_RESERVATION_STATE_CONFLICT") throw NeedsReconciliation();
        throw UploadFailure.Dependency("Billing");
    }

    private static UploadFailure NeedsReconciliation() => new(409, "ERR_REQUEST_IN_PROGRESS",
        "Reservation đã có trạng thái cuối; cần đối soát trước khi tiếp tục yêu cầu.", true);

    private sealed record BillingOwner(string Type, Guid Id);
    private sealed record ReservationReply(Guid ReservationId, Guid AnalysisId, int Generation, int Units, string? State, DateTimeOffset CreatedAt);
    private sealed record BillingError(ErrorInfo? Error);
    private sealed record ErrorInfo(string? Code);
}
