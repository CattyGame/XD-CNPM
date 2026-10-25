using System.Security.Cryptography;
using System.Text;

namespace AURA.Billing.Api.Models;

public sealed record BillingOwnerRequest(string Type, Guid Id);

public sealed record ReserveCreditRequest(
    Guid AnalysisId,
    Guid TenantId,
    Guid ActorAccountId,
    Guid PatientId,
    BillingOwnerRequest BillingOwner,
    int Units,
    int Generation);

public sealed record ReleaseCreditRequest(string Reason);

public sealed record ReservationResponse(
    Guid ReservationId,
    Guid AnalysisId,
    int Generation,
    int Units,
    string State,
    DateTimeOffset CreatedAt);

public sealed record BillingErrorDetails(
    string Code,
    string Message,
    string CorrelationId,
    bool Retryable);

public sealed record BillingErrorEnvelope(BillingErrorDetails Error);

public enum ReservationOperationStatus
{
    Created,
    Changed,
    Replayed,
    InsufficientCredit,
    Conflict,
    IdempotencyConflict,
    NotFound,
    MockLiveBlocked
}

public sealed record ReservationOperationResult(
    ReservationOperationStatus Status,
    ReservationResponse? Reservation = null);

public sealed record BillingRuntimeOptions(string Environment, bool IsMock);

public static class ReservationFingerprint
{
    public static string ForReserve(ReserveCreditRequest request, BillingRuntimeOptions options)
    {
        var canonical = string.Join('|',
            "reserve-v1",
            request.TenantId.ToString("D"),
            request.AnalysisId.ToString("D"),
            request.ActorAccountId.ToString("D"),
            request.PatientId.ToString("D"),
            request.BillingOwner.Type.Trim().ToUpperInvariant(),
            request.BillingOwner.Id.ToString("D"),
            request.Units.ToString(System.Globalization.CultureInfo.InvariantCulture),
            request.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture),
            options.Environment,
            options.IsMock ? "mock" : "non-mock");
        return Hash(canonical);
    }

    public static string ForCapture(Guid reservationId) =>
        Hash($"capture-v1|{reservationId:D}");

    public static string ForRelease(Guid reservationId, string reason) =>
        Hash($"release-v1|{reservationId:D}|{reason.Trim().ToUpperInvariant()}");

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
