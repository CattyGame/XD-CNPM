using AURA.Billing.Api.Data;
using AURA.Billing.Api.Models;

namespace AURA.Billing.Api.Services;

public sealed record ReconciliationReport(
    int TotalScanned,
    int ReleasedCount,
    int CreditsRefunded,
    IReadOnlyList<Guid> ReleasedReservations);

public sealed class StaleReservationReconciliationService(
    CreditReservationRepository repository,
    ILogger<StaleReservationReconciliationService> logger)
{
    public const string AutoRefundReason = "STALE_RESERVATION_AUTO_REFUND";

    public async Task<ReconciliationReport> ReconcileStaleReservationsAsync(
        int thresholdMinutes = 30,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var staleReservations = await repository.GetStaleReservationsAsync(
            thresholdMinutes, limit, cancellationToken);

        if (staleReservations.Count == 0)
        {
            return new ReconciliationReport(0, 0, 0, Array.Empty<Guid>());
        }

        logger.LogInformation(
            "Reconciliation scan found {Count} stale reservation(s) older than {Threshold} minutes.",
            staleReservations.Count, thresholdMinutes);

        var releasedIds = new List<Guid>();
        var totalCreditsRefunded = 0;

        foreach (var res in staleReservations)
        {
            try
            {
                var idempotencyKey = Guid.NewGuid();
                var fingerprint = ReservationFingerprint.ForRelease(res.ReservationId, AutoRefundReason);

                var result = await repository.ReleaseAsync(
                    res.ReservationId,
                    idempotencyKey,
                    fingerprint,
                    AutoRefundReason,
                    cancellationToken);

                if (result.Status == ReservationOperationStatus.Changed)
                {
                    releasedIds.Add(res.ReservationId);
                    totalCreditsRefunded += res.Units;

                    logger.LogWarning(
                        "[RECONCILIATION] Successfully auto-released stale reservation {ReservationId} (AnalysisId: {AnalysisId}, Wallet: {WalletId}, Units: {Units}, StuckSince: {UpdatedAt}). Reason: {Reason}.",
                        res.ReservationId, res.AnalysisId, res.WalletId, res.Units, res.UpdatedAt, AutoRefundReason);
                }
                else if (result.Status == ReservationOperationStatus.Replayed)
                {
                    logger.LogInformation(
                        "Stale reservation {ReservationId} was already released concurrently.",
                        res.ReservationId);
                }
                else
                {
                    logger.LogWarning(
                        "Reconciliation could not release reservation {ReservationId}. Status: {Status}",
                        res.ReservationId, result.Status);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Error while reconciling stale reservation {ReservationId}.", res.ReservationId);
            }
        }

        return new ReconciliationReport(
            staleReservations.Count,
            releasedIds.Count,
            totalCreditsRefunded,
            releasedIds);
    }
}
