using AURA.Billing.Api.Data;
using AURA.Billing.Api.Models;

namespace AURA.Billing.Api.Services;

public enum SettlementAction
{
    None,
    Capture,
    Release
}

public sealed record SettlementEventContext(
    Guid EventId,
    Guid AnalysisId,
    Guid TenantId,
    Guid ReservationId,
    int Generation,
    string ProcessingStatus,
    bool IsTerminal,
    bool HasValidResult,
    bool IsRetryable,
    string? FailureReason,
    bool IsMock);

public sealed record SettlementResolution(
    bool Applicable,
    SettlementAction Action,
    string Reason,
    Guid? ReservationId,
    int? Generation,
    string ProcessingStatus);

public sealed record SettlementExecutionResult(
    bool Succeeded,
    SettlementAction Action,
    ReservationResponse? Reservation,
    string? ErrorCode,
    string? ErrorMessage,
    bool IsReplayed);

/// <summary>
/// Domain settlement service implementing the authoritative Billing credit decision table.
/// Encapsulates event-to-action resolution and atomic execution of capture/release operations.
/// </summary>
public sealed class CreditSettlementService(
    CreditReservationRepository repository,
    ILogger<CreditSettlementService> logger)
{
    /// <summary>
    /// Resolves the required billing settlement action from an analysis event context.
    /// Strictly adheres to the authoritative decision table in docs/api/M3_Sprint3_billing_decision_table_and_contract.md.
    /// </summary>
    public SettlementResolution Resolve(SettlementEventContext context)
    {
        var status = context.ProcessingStatus.Trim().ToUpperInvariant();

        // 1. Success path: COMPLETED with a valid result -> CAPTURE exactly once
        if (status == "COMPLETED" && context.HasValidResult)
        {
            return new SettlementResolution(
                Applicable: true,
                Action: SettlementAction.Capture,
                Reason: "VALID_RESULT",
                ReservationId: context.ReservationId,
                Generation: context.Generation,
                ProcessingStatus: "COMPLETED"
            );
        }

        // 2. Intermediate retry path: Retryable failure -> keep reservation RESERVED (do not release)
        if (context.IsRetryable && !context.IsTerminal)
        {
            logger.LogInformation(
                "Analysis {AnalysisId} reported retryable failure; keeping reservation {ReservationId} in RESERVED state.",
                context.AnalysisId, context.ReservationId);

            return new SettlementResolution(
                Applicable: false,
                Action: SettlementAction.None,
                Reason: "NOT_TERMINAL",
                ReservationId: context.ReservationId,
                Generation: context.Generation,
                ProcessingStatus: status
            );
        }

        // 3. Terminal failure paths: Release exactly once, returning credit to the wallet
        if (status == "LOW_QUALITY" || string.Equals(context.FailureReason, "LOW_QUALITY", StringComparison.OrdinalIgnoreCase))
        {
            return new SettlementResolution(
                Applicable: true,
                Action: SettlementAction.Release,
                Reason: "LOW_QUALITY",
                ReservationId: context.ReservationId,
                Generation: context.Generation,
                ProcessingStatus: "LOW_QUALITY"
            );
        }

        if (status is "FAILED" or "REJECTED" || context.IsTerminal)
        {
            var releaseReason = NormalizeReleaseReason(context.FailureReason);
            return new SettlementResolution(
                Applicable: true,
                Action: SettlementAction.Release,
                Reason: releaseReason,
                ReservationId: context.ReservationId,
                Generation: context.Generation,
                ProcessingStatus: "FAILED"
            );
        }

        // 4. Default: No settlement action for active/transient states
        return new SettlementResolution(
            Applicable: false,
            Action: SettlementAction.None,
            Reason: "NOT_TERMINAL",
            ReservationId: context.ReservationId,
            Generation: context.Generation,
            ProcessingStatus: status
        );
    }

    /// <summary>
    /// Executes the resolved settlement action atomically against the credit reservation repository.
    /// </summary>
    public async Task<SettlementExecutionResult> ExecuteAsync(
        SettlementResolution resolution,
        Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!resolution.Applicable || resolution.Action == SettlementAction.None || resolution.ReservationId is null)
        {
            return new SettlementExecutionResult(true, SettlementAction.None, null, null, null, false);
        }

        var reservationId = resolution.ReservationId.Value;

        if (resolution.Action == SettlementAction.Capture)
        {
            var fingerprint = ReservationFingerprint.ForCapture(reservationId);
            var result = await repository.CaptureAsync(reservationId, idempotencyKey, fingerprint, cancellationToken);

            return MapRepositoryResult(result, SettlementAction.Capture);
        }

        if (resolution.Action == SettlementAction.Release)
        {
            var reason = resolution.Reason;
            var fingerprint = ReservationFingerprint.ForRelease(reservationId, reason);
            var result = await repository.ReleaseAsync(reservationId, idempotencyKey, fingerprint, reason, cancellationToken);

            return MapRepositoryResult(result, SettlementAction.Release);
        }

        return new SettlementExecutionResult(false, resolution.Action, null, "ERR_UNKNOWN_ACTION", "Hành động settlement không xác định.", false);
    }

    private static string NormalizeReleaseReason(string? rawReason)
    {
        var reason = rawReason?.Trim().ToUpperInvariant();
        return reason switch
        {
            "VALIDATION_ABORT" => "VALIDATION_ABORT",
            "STORAGE_ABORT" => "STORAGE_ABORT",
            "ANALYSIS_ABORT" => "ANALYSIS_ABORT",
            "LOW_QUALITY" => "LOW_QUALITY",
            _ => "FINAL_FAILURE"
        };
    }

    private static SettlementExecutionResult MapRepositoryResult(ReservationOperationResult result, SettlementAction action)
    {
        return result.Status switch
        {
            ReservationOperationStatus.Changed => new SettlementExecutionResult(
                true, action, result.Reservation, null, null, false),

            ReservationOperationStatus.Replayed => new SettlementExecutionResult(
                true, action, result.Reservation, null, null, true),

            ReservationOperationStatus.NotFound => new SettlementExecutionResult(
                false, action, null, "ERR_NOT_FOUND", "Không tìm thấy reservation.", false),

            ReservationOperationStatus.MockLiveBlocked => new SettlementExecutionResult(
                false, action, result.Reservation, "ERR_MOCK_LIVE_FORBIDDEN", "Mock result không được capture vào ví LIVE.", false),

            ReservationOperationStatus.IdempotencyConflict => new SettlementExecutionResult(
                false, action, null, "ERR_IDEMPOTENCY_CONFLICT", "Idempotency-Key đã được dùng cho một yêu cầu khác.", false),

            ReservationOperationStatus.Conflict => new SettlementExecutionResult(
                false, action, null, "ERR_RESERVATION_STATE_CONFLICT", "Trạng thái reservation không cho phép thao tác này.", false),

            _ => new SettlementExecutionResult(
                false, action, null, "ERR_UNEXPECTED", "Lỗi trạng thái reservation bất ngờ.", false)
        };
    }
}
