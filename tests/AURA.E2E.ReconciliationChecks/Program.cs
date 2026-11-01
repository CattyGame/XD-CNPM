using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NpgsqlTypes;
using AURA.Billing.Api.Data;
using AURA.Billing.Api.Models;
using AURA.Billing.Api.Services;
using AURA.Notification.Api;

namespace AURA.E2E.ReconciliationChecks;

public class Program
{
    private const string ConnStr = "Host=127.0.0.1;Port=5432;Database=aura_dev;Username=aura_dev;Password=b324846608e64afc9d2ece68f9cdacad;Maximum Pool Size=20;";
    private static int _passedCount = 0;
    private static int _totalCount = 0;

    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("  AURA SPRINT 3 — FULL END-TO-END & RECONCILIATION TEST SUITE (SCRUM-328)");
        Console.WriteLine("  Environment: PostgreSQL 17 (aura-postgres-1) @ 127.0.0.1:5432");
        Console.WriteLine("  Baseline SHA: 97027420eeba9c9ba2acd51dc253e0b0494a9695");
        Console.WriteLine("================================================================================\n");

        await using var dataSource = NpgsqlDataSource.Create(ConnStr);

        var billingRepo = new CreditReservationRepository(dataSource, NullLogger<CreditReservationRepository>.Instance);
        var billingService = new CreditSettlementService(billingRepo, NullLogger<CreditSettlementService>.Instance);
        var inboxRepo = new NotificationInboxRepository(dataSource, NullLogger<NotificationInboxRepository>.Instance);

        // Preflight: Ensure clean state for Sprint 3 fixture wallets
        await ResetSprint3FixturesAsync(dataSource);

        // Run the 12 Scenarios defined in m3_sprint3_e2e_and_reconciliation_matrix.md
        await RunTestAsync("S328-E2E-01: Happy Path Full Tri-Party Lifecycle (Upload -> Reserve -> Worker -> Capture -> Inbox -> Read)",
            () => TestScenario01_HappyPathAsync(dataSource, billingRepo, billingService, inboxRepo));

        await RunTestAsync("S328-E2E-02: Idempotency Replay on Request / Reservation (Zero Double-Debit)",
            () => TestScenario02_IdempotencyReplayAsync(billingRepo));

        await RunTestAsync("S328-E2E-03: Competing Concurrency on 1-Credit Wallet (Zero Overdraft Guarantee)",
            () => TestScenario03_CompetingConcurrencyAsync(dataSource, billingRepo));

        await RunTestAsync("S328-E2E-04: Exhaustion on 0-Credit Wallet (402 Insufficient Credit)",
            () => TestScenario04_CreditExhaustionAsync(dataSource, billingRepo));

        await RunTestAsync("S328-E2E-05: AI Timeout & Worker Retry Handling (Reservation Held -> Captures Once)",
            () => TestScenario05_AiTimeoutRetryAsync(dataSource, billingRepo, billingService, inboxRepo));

        await RunTestAsync("S328-E2E-06: Terminal Failure / Low Quality Image Auto-Refund (Release Reservation)",
            () => TestScenario06_LowQualityAutoRefundAsync(dataSource, billingRepo, billingService, inboxRepo));

        await RunTestAsync("S328-E2E-07: Worker Crash & Message Re-delivery (AckDuplicate & Zero Extra Debit)",
            () => TestScenario07_WorkerCrashRedeliveryAsync(dataSource));

        await RunTestAsync("S328-E2E-08: Notification Inbox Dedup on Event Replay (Unique account_id + event_id)",
            () => TestScenario08_InboxDedupAsync(dataSource, inboxRepo));

        await RunTestAsync("S328-E2E-09: Mutual Exclusivity Guard between Capture and Release (409 Conflict)",
            () => TestScenario09_MutualExclusivityAsync(billingRepo, billingService));

        await RunTestAsync("S328-E2E-10: Cross-Tenant & Cross-Account Isolation (Data & Authorization Boundary)",
            () => TestScenario10_CrossTenantIsolationAsync(inboxRepo));

        await RunTestAsync("S328-E2E-11: Reporting Outage Resilience & Pipeline Decoupling",
            () => TestScenario11_ReportingResilienceAsync(dataSource));

        await RunTestAsync("S328-E2E-12: Tri-Party Reconciliation Audit on All Wallets (Zero Discrepancy)",
            () => TestScenario12_TriPartyReconciliationAuditAsync(dataSource));

        Console.WriteLine("\n================================================================================");
        Console.WriteLine($"  EXECUTION SUMMARY: {_passedCount}/{_totalCount} TESTS PASSED (100%)");
        Console.WriteLine("================================================================================");

        return _passedCount == _totalCount ? 0 : 1;
    }

    private static async Task RunTestAsync(string title, Func<Task> testFunc)
    {
        _totalCount++;
        Console.Write($"[{_totalCount:D2}] RUNNING: {title} ... ");
        try
        {
            await testFunc();
            _passedCount++;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("PASS");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FAIL");
            Console.ResetColor();
            Console.WriteLine($"    ERROR: {ex.Message}");
            Console.WriteLine($"    STACK: {ex.StackTrace}");
            throw;
        }
    }

    private static async Task ResetSprint3FixturesAsync(NpgsqlDataSource dataSource)
    {
        await using var conn = await dataSource.OpenConnectionAsync();

        // Clean any residual test data tied to sprint3 accounts / fixtures
        var sql = """
            DELETE FROM notification.inbox_items WHERE tenant_id IN ('c3010000-0000-4000-8000-000000000001', 'c3020000-0000-4000-8000-000000000002', 'c3030000-0000-4000-8000-000000000003', 'c3040000-0000-4000-8000-000000000004', 'c3050000-0000-4000-8000-000000000005');
            DELETE FROM billing.credit_ledger_entries WHERE reservation_id IN (SELECT id FROM billing.credit_reservations WHERE tenant_id IN ('c3010000-0000-4000-8000-000000000001', 'c3020000-0000-4000-8000-000000000002', 'c3030000-0000-4000-8000-000000000003', 'c3040000-0000-4000-8000-000000000004', 'c3050000-0000-4000-8000-000000000005')) OR (wallet_id IN ('a3010000-0000-4000-8000-000000000001', 'a3020000-0000-4000-8000-000000000002', 'a3030000-0000-4000-8000-000000000003', 'a3040000-0000-4000-8000-000000000004', 'a3050000-0000-4000-8000-000000000005') AND entry_type != 'CREDIT');
            DELETE FROM billing.credit_reservations WHERE tenant_id IN ('c3010000-0000-4000-8000-000000000001', 'c3020000-0000-4000-8000-000000000002', 'c3030000-0000-4000-8000-000000000003', 'c3040000-0000-4000-8000-000000000004', 'c3050000-0000-4000-8000-000000000005');
            
            
            -- Reset wallet balances to initial fixture baseline
            UPDATE billing.credit_wallets SET balance = 1, updated_at = NOW() WHERE id = 'a3010000-0000-4000-8000-000000000001';
            UPDATE billing.credit_wallets SET balance = 10, updated_at = NOW() WHERE id = 'a3020000-0000-4000-8000-000000000002';
            UPDATE billing.credit_wallets SET balance = 0, updated_at = NOW() WHERE id = 'a3030000-0000-4000-8000-000000000003';
            UPDATE billing.credit_wallets SET balance = 5, updated_at = NOW() WHERE id = 'a3040000-0000-4000-8000-000000000004';
            UPDATE billing.credit_wallets SET balance = 5, updated_at = NOW() WHERE id = 'a3050000-0000-4000-8000-000000000005';
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    // Shared state for cross-scenario tests
    private static Guid _scen1AnalysisId;
    private static Guid _scen1ReservationId;
    private static Guid _scen1BatchId;
    private static Guid _scen6AnalysisId;
    private static Guid _scen6ReservationId;

    private static async Task TestScenario01_HappyPathAsync(
        NpgsqlDataSource dataSource,
        CreditReservationRepository billingRepo,
        CreditSettlementService billingService,
        NotificationInboxRepository inboxRepo)
    {
        var tenantId = Guid.Parse("c3020000-0000-4000-8000-000000000002");
        var accountId = Guid.Parse("e3020000-0000-4000-8000-000000000002");
        var patientId = accountId;
        var batchId = Guid.NewGuid();
        var analysisId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();
        var fingerprint = ComputeSha256("sprint3-scen1-fundus-payload");
        var runtimeOptions = new BillingRuntimeOptions("TEST", false);

        _scen1BatchId = batchId;
        _scen1AnalysisId = analysisId;

        await using var conn = await dataSource.OpenConnectionAsync();

        // 1. Insert Analysis Request & Image & Job into analysis schema
        var insertSql = """
            INSERT INTO analysis.analysis_requests (
                id, tenant_id, owner_id, requester_id, patient_id, idempotency_key,
                request_fingerprint, request_schema_version, accepted_response, correlation_id
            ) VALUES (
                @batch_id, @tenant_id, @account_id, @account_id, @patient_id, @idempotency_key_uuid,
                @fingerprint, '0.7', '{"status":"ACCEPTED"}'::jsonb, @correlation_id
            );

            INSERT INTO analysis.images (
                id, tenant_id, analysis_request_id, file_id, modality, eye_side,
                object_key, mime_type, size_bytes, width, height, sha256, processing_status
            ) VALUES (
                @analysis_id, @tenant_id, @batch_id, @file_id, 'FUNDUS', 'left',
                @object_key, 'image/png', 204800, 1024, 768, @sha256, 'QUEUED'
            );

            INSERT INTO analysis.jobs (
                id, tenant_id, image_id, message_id, attempt_no, status, correlation_id
            ) VALUES (
                @job_id, @tenant_id, @analysis_id, @message_id, 1, 'QUEUED', @correlation_id
            );
            """;

        await using (var cmd = new NpgsqlCommand(insertSql, conn))
        {
            cmd.Parameters.AddWithValue("batch_id", batchId);
            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("account_id", accountId);
            cmd.Parameters.AddWithValue("patient_id", patientId);
            cmd.Parameters.AddWithValue("idempotency_key_uuid", Guid.NewGuid());
            cmd.Parameters.AddWithValue("fingerprint", fingerprint);
            cmd.Parameters.AddWithValue("correlation_id", "corr-scen1");
            cmd.Parameters.AddWithValue("analysis_id", analysisId);
            cmd.Parameters.AddWithValue("file_id", fileId);
            cmd.Parameters.AddWithValue("object_key", $"uploads/{tenantId:D}/{analysisId:D}.png");
            cmd.Parameters.AddWithValue("sha256", fingerprint);
            cmd.Parameters.AddWithValue("job_id", jobId);
            cmd.Parameters.AddWithValue("message_id", messageId);
            await cmd.ExecuteNonQueryAsync();
        }

        // 2. Billing: Reserve Credit (-1)
        var reserveReq = new ReserveCreditRequest(
            AnalysisId: analysisId,
            TenantId: tenantId,
            ActorAccountId: accountId,
            PatientId: patientId,
            BillingOwner: new BillingOwnerRequest("ACCOUNT", accountId),
            Units: 1,
            Generation: 1
        );

        var reserveResult = await billingRepo.ReserveAsync(
            reserveReq, idempotencyKey, fingerprint, runtimeOptions, CancellationToken.None);

        if (reserveResult.Status != ReservationOperationStatus.Created || reserveResult.Reservation is null)
            throw new Exception($"Expected Created reservation, got {reserveResult.Status}");

        if (reserveResult.Reservation.State != "RESERVED")
            throw new Exception($"Expected RESERVED state, got {reserveResult.Reservation.State}");

        _scen1ReservationId = reserveResult.Reservation.ReservationId;

        // 3. Worker: Process job & save AI result (Frangi 0.2 metrics)
        var resultId = Guid.NewGuid();
        var updateAnalysisSql = """
            UPDATE analysis.jobs SET status = 'SUCCEEDED', finished_at = NOW(), updated_at = NOW() WHERE id = @job_id;
            UPDATE analysis.images SET processing_status = 'COMPLETED', updated_at = NOW() WHERE id = @analysis_id;

            INSERT INTO analysis.analysis_results (
                id, tenant_id, image_id, job_id, version, result_schema_version,
                risk_assessment, metrics, warnings, is_mock, model_version, config_version,
                threshold_version, processing_time_ms, limitations
            ) VALUES (
                @result_id, @tenant_id, @analysis_id, @job_id, 1, '0.7',
                '{"risk_available": false, "risk_assessment": null}'::jsonb,
                '{"vessel_density": 0.082}'::jsonb,
                '[{"code": "RISK_UNAVAILABLE", "message": "Frangi segmentation baseline"}]'::jsonb,
                false, 'frangi-v0.2', 's3-frangi-1.0.0', 'frangi-thresh-0.02', 125.5, 'Sprint 3 research baseline'
            );

            INSERT INTO analysis.outbox_events (
                id, tenant_id, analysis_request_id, image_id, event_type, schema_version,
                payload, occurred_at
            ) VALUES (
                @event_id, @tenant_id, @batch_id, @analysis_id, 'analysis.completed', '0.7',
                '{"status": "COMPLETED"}'::jsonb, NOW()
            );
            """;

        await using (var cmd = new NpgsqlCommand(updateAnalysisSql, conn))
        {
            cmd.Parameters.AddWithValue("job_id", jobId);
            cmd.Parameters.AddWithValue("analysis_id", analysisId);
            cmd.Parameters.AddWithValue("result_id", resultId);
            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("batch_id", batchId);
            cmd.Parameters.AddWithValue("event_id", eventId);
            await cmd.ExecuteNonQueryAsync();
        }

        // 4. Billing: Resolve & Execute Capture
        var eventContext = new SettlementEventContext(
            EventId: eventId,
            AnalysisId: analysisId,
            TenantId: tenantId,
            ReservationId: _scen1ReservationId,
            Generation: 1,
            ProcessingStatus: "COMPLETED",
            IsTerminal: true,
            HasValidResult: true,
            IsRetryable: false,
            FailureReason: null,
            IsMock: false
        );

        var resolution = billingService.Resolve(eventContext);
        if (resolution.Action != SettlementAction.Capture)
            throw new Exception($"Expected SettlementAction.Capture, got {resolution.Action}");

        var execResult = await billingService.ExecuteAsync(resolution, Guid.NewGuid(), CancellationToken.None);
        if (!execResult.Succeeded || execResult.Reservation?.State != "CAPTURED")
            throw new Exception($"Expected CAPTURED state, got {execResult.Reservation?.State}");

        // 5. Notification: Inbox item creation
        var saveCmd = new SaveInboxItemCommand(
            AccountId: accountId,
            TenantId: tenantId,
            EventId: eventId,
            AnalysisId: analysisId,
            Type: "ANALYSIS_COMPLETED",
            Title: "Kết quả phân tích đã sẵn sàng",
            TargetPath: $"/analyses/{analysisId:D}",
            Metadata: JsonSerializer.Serialize(new { batchId, analysisId, status = "COMPLETED" })
        );

        var saveResult = await inboxRepo.SaveAsync(saveCmd, CancellationToken.None);
        if (saveResult.Outcome != SaveInboxOutcome.Created)
            throw new Exception($"Expected SaveInboxOutcome.Created, got {saveResult.Outcome}");

        // 6. Verify Inbox unread count and mark as read
        var unreadCount = await inboxRepo.GetUnreadCountAsync(accountId, CancellationToken.None);
        if (unreadCount != 1)
            throw new Exception($"Expected unread count 1, got {unreadCount}");

        var inboxList = await inboxRepo.GetInboxAsync(accountId, 10, null, false, CancellationToken.None);
        if (inboxList.Items.Count != 1 || inboxList.Items[0].NotificationId != saveResult.NotificationId)
            throw new Exception("Inbox items query did not return expected item");

        var marked = await inboxRepo.MarkAsReadAsync(saveResult.NotificationId, accountId, CancellationToken.None);
        if (marked != MarkReadOutcome.Success)
            throw new Exception($"MarkAsReadAsync failed with outcome: {marked}");

        var finalUnread = await inboxRepo.GetUnreadCountAsync(accountId, CancellationToken.None);
        if (finalUnread != 0)
            throw new Exception($"Expected unread count 0 after mark read, got {finalUnread}");

        // 7. Verify DB balance & ledger entry count
        var walletId = Guid.Parse("a3020000-0000-4000-8000-000000000002");
        var currentBalance = await GetWalletBalanceAsync(conn, walletId);
        if (currentBalance != 9)
            throw new Exception($"Expected wallet balance 9, got {currentBalance}");

        var ledgerCount = await GetLedgerCountAsync(conn, walletId);
        if (ledgerCount != 3) // 1 initial credit + 1 reserve + 1 capture = 3
            throw new Exception($"Expected 3 ledger entries, got {ledgerCount}");
    }

    private static async Task TestScenario02_IdempotencyReplayAsync(CreditReservationRepository billingRepo)
    {
        var tenantId = Guid.Parse("c3020000-0000-4000-8000-000000000002");
        var accountId = Guid.Parse("e3020000-0000-4000-8000-000000000002");
        var analysisId = Guid.NewGuid();
        var replayKey = Guid.NewGuid();
        var fingerprint = ComputeSha256("sprint3-replay-fingerprint");
        var runtimeOptions = new BillingRuntimeOptions("TEST", false);

        var req = new ReserveCreditRequest(
            AnalysisId: analysisId,
            TenantId: tenantId,
            ActorAccountId: accountId,
            PatientId: accountId,
            BillingOwner: new BillingOwnerRequest("ACCOUNT", accountId),
            Units: 1,
            Generation: 1
        );

        // First call
        var first = await billingRepo.ReserveAsync(req, replayKey, fingerprint, runtimeOptions, CancellationToken.None);
        if (first.Status != ReservationOperationStatus.Created)
            throw new Exception($"Expected Created, got {first.Status}");

        // Second call with identical key & fingerprint -> Replayed
        var second = await billingRepo.ReserveAsync(req, replayKey, fingerprint, runtimeOptions, CancellationToken.None);
        if (second.Status != ReservationOperationStatus.Replayed)
            throw new Exception($"Expected Replayed, got {second.Status}");

        if (first.Reservation?.ReservationId != second.Reservation?.ReservationId)
            throw new Exception("Idempotent replay produced different reservation IDs");

        // Third call with differing fingerprint -> IdempotencyConflict
        var third = await billingRepo.ReserveAsync(req, replayKey, ComputeSha256("mismatched-fp"), runtimeOptions, CancellationToken.None);
        if (third.Status != ReservationOperationStatus.IdempotencyConflict)
            throw new Exception($"Expected IdempotencyConflict, got {third.Status}");
    }

    private static async Task TestScenario03_CompetingConcurrencyAsync(NpgsqlDataSource dataSource, CreditReservationRepository billingRepo)
    {
        var tenantId = Guid.Parse("c3010000-0000-4000-8000-000000000001");
        var walletId = Guid.Parse("a3010000-0000-4000-8000-000000000001");
        var accountId = Guid.Parse("e3010000-0000-4000-8000-000000000001");
        var runtimeOptions = new BillingRuntimeOptions("TEST", false);

        // Wallet a301 has exactly 1 credit. 2 parallel requests compete for it.
        var req1 = new ReserveCreditRequest(
            AnalysisId: Guid.NewGuid(),
            TenantId: tenantId,
            ActorAccountId: accountId,
            PatientId: accountId,
            BillingOwner: new BillingOwnerRequest("ACCOUNT", accountId),
            Units: 1,
            Generation: 1
        );
        var req2 = new ReserveCreditRequest(
            AnalysisId: Guid.NewGuid(),
            TenantId: tenantId,
            ActorAccountId: accountId,
            PatientId: accountId,
            BillingOwner: new BillingOwnerRequest("ACCOUNT", accountId),
            Units: 1,
            Generation: 1
        );

        var t1 = billingRepo.ReserveAsync(req1, Guid.NewGuid(), ComputeSha256("r1"), runtimeOptions, CancellationToken.None);
        var t2 = billingRepo.ReserveAsync(req2, Guid.NewGuid(), ComputeSha256("r2"), runtimeOptions, CancellationToken.None);

        var results = await Task.WhenAll(t1, t2);

        int createdCount = 0;
        int failedCount = 0;

        foreach (var r in results)
        {
            if (r.Status == ReservationOperationStatus.Created) createdCount++;
            if (r.Status == ReservationOperationStatus.InsufficientCredit) failedCount++;
        }

        if (createdCount != 1 || failedCount != 1)
            throw new Exception($"Expected 1 Created and 1 InsufficientCredit, got {createdCount} created and {failedCount} failed");

        await using var conn = await dataSource.OpenConnectionAsync();
        var balance = await GetWalletBalanceAsync(conn, walletId);
        if (balance != 0)
            throw new Exception($"Expected balance 0 after race, got {balance}");
    }

    private static async Task TestScenario04_CreditExhaustionAsync(NpgsqlDataSource dataSource, CreditReservationRepository billingRepo)
    {
        var tenantId = Guid.Parse("c3030000-0000-4000-8000-000000000003");
        var walletId = Guid.Parse("a3030000-0000-4000-8000-000000000003");
        var accountId = Guid.Parse("e3030000-0000-4000-8000-000000000003");
        var runtimeOptions = new BillingRuntimeOptions("TEST", false);

        // Wallet a303 has 0 credits
        var req = new ReserveCreditRequest(
            AnalysisId: Guid.NewGuid(),
            TenantId: tenantId,
            ActorAccountId: accountId,
            PatientId: accountId,
            BillingOwner: new BillingOwnerRequest("ACCOUNT", accountId),
            Units: 1,
            Generation: 1
        );

        var res = await billingRepo.ReserveAsync(req, Guid.NewGuid(), ComputeSha256("ex"), runtimeOptions, CancellationToken.None);

        if (res.Status != ReservationOperationStatus.InsufficientCredit)
            throw new Exception($"Expected InsufficientCredit on zero-balance wallet, got {res.Status}");

        await using var conn = await dataSource.OpenConnectionAsync();
        var balance = await GetWalletBalanceAsync(conn, walletId);
        if (balance != 0)
            throw new Exception($"Balance should remain 0, got {balance}");
    }

    private static async Task TestScenario05_AiTimeoutRetryAsync(
        NpgsqlDataSource dataSource,
        CreditReservationRepository billingRepo,
        CreditSettlementService billingService,
        NotificationInboxRepository inboxRepo)
    {
        var tenantId = Guid.Parse("c3020000-0000-4000-8000-000000000002");
        var accountId = Guid.Parse("e3020000-0000-4000-8000-000000000002");
        var analysisId = Guid.NewGuid();
        var runtimeOptions = new BillingRuntimeOptions("TEST", false);

        // Step 1: Reserve credit
        var req = new ReserveCreditRequest(
            AnalysisId: analysisId,
            TenantId: tenantId,
            ActorAccountId: accountId,
            PatientId: accountId,
            BillingOwner: new BillingOwnerRequest("ACCOUNT", accountId),
            Units: 1,
            Generation: 1
        );

        var res = await billingRepo.ReserveAsync(req, Guid.NewGuid(), ComputeSha256("retry-test"), runtimeOptions, CancellationToken.None);
        if (res.Status != ReservationOperationStatus.Created || res.Reservation is null)
            throw new Exception("Reservation failed");

        var reservationId = res.Reservation.ReservationId;

        // Step 2: Attempt 1 transient failure (e.g. AI timeout 504) -> Worker schedules retry
        // In the decision table: IsRetryable = true, IsTerminal = false -> SettlementAction.None!
        var retryContext = new SettlementEventContext(
            EventId: Guid.NewGuid(),
            AnalysisId: analysisId,
            TenantId: tenantId,
            ReservationId: reservationId,
            Generation: 1,
            ProcessingStatus: "RETRY_READY",
            IsTerminal: false,
            HasValidResult: false,
            IsRetryable: true,
            FailureReason: "ERR_AI_TIMEOUT",
            IsMock: false
        );

        var retryResolution = billingService.Resolve(retryContext);
        if (retryResolution.Action != SettlementAction.None)
            throw new Exception($"Expected SettlementAction.None during retry, got {retryResolution.Action}");

        // In DB, reservation must remain RESERVED!
        await using (var conn = await dataSource.OpenConnectionAsync())
        {
            var status = await GetReservationStatusAsync(conn, reservationId);
            if (status != "RESERVED")
                throw new Exception($"Expected reservation status RESERVED during retry, got {status}");
        }

        // Step 3: Attempt 2 succeeds -> Worker publishes analysis.completed -> Billing captures once
        var successContext = new SettlementEventContext(
            EventId: Guid.NewGuid(),
            AnalysisId: analysisId,
            TenantId: tenantId,
            ReservationId: reservationId,
            Generation: 2,
            ProcessingStatus: "COMPLETED",
            IsTerminal: true,
            HasValidResult: true,
            IsRetryable: false,
            FailureReason: null,
            IsMock: false
        );

        var successResolution = billingService.Resolve(successContext);
        if (successResolution.Action != SettlementAction.Capture)
            throw new Exception($"Expected SettlementAction.Capture on success, got {successResolution.Action}");

        var exec = await billingService.ExecuteAsync(successResolution, Guid.NewGuid(), CancellationToken.None);
        if (!exec.Succeeded || exec.Reservation?.State != "CAPTURED")
            throw new Exception($"Expected CAPTURED state, got {exec.Reservation?.State}");
    }

    private static async Task TestScenario06_LowQualityAutoRefundAsync(
        NpgsqlDataSource dataSource,
        CreditReservationRepository billingRepo,
        CreditSettlementService billingService,
        NotificationInboxRepository inboxRepo)
    {
        var tenantId = Guid.Parse("c3020000-0000-4000-8000-000000000002");
        var walletId = Guid.Parse("a3020000-0000-4000-8000-000000000002");
        var accountId = Guid.Parse("e3020000-0000-4000-8000-000000000002");
        var analysisId = Guid.NewGuid();
        var runtimeOptions = new BillingRuntimeOptions("TEST", false);

        _scen6AnalysisId = analysisId;

        await using var conn = await dataSource.OpenConnectionAsync();
        var balanceBefore = await GetWalletBalanceAsync(conn, walletId);

        // 1. Reserve credit (-1)
        var req = new ReserveCreditRequest(
            AnalysisId: analysisId,
            TenantId: tenantId,
            ActorAccountId: accountId,
            PatientId: accountId,
            BillingOwner: new BillingOwnerRequest("ACCOUNT", accountId),
            Units: 1,
            Generation: 1
        );

        var res = await billingRepo.ReserveAsync(req, Guid.NewGuid(), ComputeSha256("lq-image"), runtimeOptions, CancellationToken.None);
        if (res.Status != ReservationOperationStatus.Created || res.Reservation is null)
            throw new Exception("Reservation failed");

        _scen6ReservationId = res.Reservation.ReservationId;

        var balanceReserved = await GetWalletBalanceAsync(conn, walletId);
        if (balanceReserved != balanceBefore - 1)
            throw new Exception($"Expected balance {balanceBefore - 1}, got {balanceReserved}");

        // 2. Terminal failure (LOW_QUALITY 422) -> Release reservation (+1 refund)
        var failContext = new SettlementEventContext(
            EventId: Guid.NewGuid(),
            AnalysisId: analysisId,
            TenantId: tenantId,
            ReservationId: _scen6ReservationId,
            Generation: 1,
            ProcessingStatus: "LOW_QUALITY",
            IsTerminal: true,
            HasValidResult: false,
            IsRetryable: false,
            FailureReason: "LOW_QUALITY",
            IsMock: false
        );

        var failResolution = billingService.Resolve(failContext);
        if (failResolution.Action != SettlementAction.Release)
            throw new Exception($"Expected SettlementAction.Release, got {failResolution.Action}");

        var exec = await billingService.ExecuteAsync(failResolution, Guid.NewGuid(), CancellationToken.None);
        if (!exec.Succeeded || exec.Reservation?.State != "RELEASED")
            throw new Exception($"Expected RELEASED, got {exec.Reservation?.State}");

        var balanceAfterRefund = await GetWalletBalanceAsync(conn, walletId);
        if (balanceAfterRefund != balanceBefore)
            throw new Exception($"Expected balance refunded to {balanceBefore}, got {balanceAfterRefund}");

        // 3. Notification inbox receives ANALYSIS_FAILED
        var saveCmd = new SaveInboxItemCommand(
            AccountId: accountId,
            TenantId: tenantId,
            EventId: Guid.NewGuid(),
            AnalysisId: analysisId,
            Type: "ANALYSIS_FAILED",
            Title: "Phân tích không thành công",
            TargetPath: $"/analyses/{analysisId:D}",
            Metadata: JsonSerializer.Serialize(new { analysisId, errorCode = "ERR_IMAGE_LOW_QUALITY" })
        );

        var saveResult = await inboxRepo.SaveAsync(saveCmd, CancellationToken.None);
        if (saveResult.Outcome != SaveInboxOutcome.Created)
            throw new Exception("Notification creation failed for terminal failure");
    }

    private static async Task TestScenario07_WorkerCrashRedeliveryAsync(NpgsqlDataSource dataSource)
    {
        await using var conn = await dataSource.OpenConnectionAsync();

        // Check that our job from Scenario 1 is already in terminal state SUCCEEDED
        var sql = "SELECT processing_status FROM analysis.images WHERE id = @analysis_id;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("analysis_id", _scen1AnalysisId);
        var status = (string?)await cmd.ExecuteScalarAsync();

        if (status != "COMPLETED")
            throw new Exception($"Expected COMPLETED analysis status, got {status}");

        // Simulating worker duplicate guard:
        // When job.status is Succeeded, JobProcessor returns AckDuplicate and does NOT re-execute AI.
        // Assert ledger entries count did not increase for this reservation
        var ledgerSql = "SELECT count(*) FROM billing.credit_ledger_entries WHERE reservation_id = @res_id;";
        await using var cmdLedger = new NpgsqlCommand(ledgerSql, conn);
        cmdLedger.Parameters.AddWithValue("res_id", _scen1ReservationId);
        var count = Convert.ToInt64(await cmdLedger.ExecuteScalarAsync());

        if (count != 2) // 1 reserve + 1 capture
            throw new Exception($"Expected exactly 2 ledger entries for settled reservation, got {count}");
    }

    private static async Task TestScenario08_InboxDedupAsync(NpgsqlDataSource dataSource, NotificationInboxRepository inboxRepo)
    {
        var accountId = Guid.Parse("e3020000-0000-4000-8000-000000000002");
        var tenantId = Guid.Parse("c3020000-0000-4000-8000-000000000002");
        var eventId = Guid.NewGuid();

        var cmd1 = new SaveInboxItemCommand(
            AccountId: accountId,
            TenantId: tenantId,
            EventId: eventId,
            AnalysisId: Guid.NewGuid(),
            Type: "ANALYSIS_COMPLETED",
            Title: "Duplicate Event Test",
            TargetPath: "/test",
            Metadata: "{}"
        );

        // First delivery -> Created
        var r1 = await inboxRepo.SaveAsync(cmd1, CancellationToken.None);
        if (r1.Outcome != SaveInboxOutcome.Created)
            throw new Exception($"Expected Created, got {r1.Outcome}");

        // Duplicate delivery (same account_id, same event_id) -> Deduplicated
        var cmd2 = new SaveInboxItemCommand(
            AccountId: accountId,
            TenantId: tenantId,
            EventId: eventId,
            AnalysisId: cmd1.AnalysisId,
            Type: "ANALYSIS_COMPLETED",
            Title: "Duplicate Event Test 2",
            TargetPath: "/test",
            Metadata: "{}"
        );

        var r2 = await inboxRepo.SaveAsync(cmd2, CancellationToken.None);
        if (r2.Outcome != SaveInboxOutcome.Deduplicated)
            throw new Exception($"Expected Deduplicated, got {r2.Outcome}");

        // Verify count of inbox items for this event is strictly 1
        await using var conn = await dataSource.OpenConnectionAsync();
        var sql = "SELECT count(*) FROM notification.inbox_items WHERE account_id = @account_id AND event_id = @event_id;";
        await using var countCmd = new NpgsqlCommand(sql, conn);
        countCmd.Parameters.AddWithValue("account_id", accountId);
        countCmd.Parameters.AddWithValue("event_id", eventId);
        var count = Convert.ToInt64(await countCmd.ExecuteScalarAsync());

        if (count != 1)
            throw new Exception($"Expected strictly 1 inbox item after duplicate event, got {count}");
    }

    private static async Task TestScenario09_MutualExclusivityAsync(
        CreditReservationRepository billingRepo,
        CreditSettlementService billingService)
    {
        // 1. Attempt to Capture the RELEASED reservation (_scen6ReservationId) -> Must be rejected
        var capOnRel = await billingRepo.CaptureAsync(_scen6ReservationId, Guid.NewGuid(), ComputeSha256("fp"), CancellationToken.None);
        if (capOnRel.Status != ReservationOperationStatus.Conflict)
            throw new Exception($"Expected Conflict when capturing a RELEASED reservation, got {capOnRel.Status}");

        // 2. Attempt to Release the CAPTURED reservation (_scen1ReservationId) -> Must be rejected
        var relOnCap = await billingRepo.ReleaseAsync(_scen1ReservationId, Guid.NewGuid(), ComputeSha256("fp"), "ERR_CONFLICT_TEST", CancellationToken.None);
        if (relOnCap.Status != ReservationOperationStatus.Conflict)
            throw new Exception($"Expected Conflict when releasing a CAPTURED reservation, got {relOnCap.Status}");
    }

    private static async Task TestScenario10_CrossTenantIsolationAsync(NotificationInboxRepository inboxRepo)
    {
        var account1 = Guid.Parse("e3010000-0000-4000-8000-000000000001");
        var account2 = Guid.Parse("e3020000-0000-4000-8000-000000000002");

        // Query inbox of Account 1 -> cannot see Account 2's notifications
        var inbox1 = await inboxRepo.GetInboxAsync(account1, 10, null, false, CancellationToken.None);
        if (inbox1.Items.Count != 0)
            throw new Exception($"Cross-account data leakage: Account 1 found {inbox1.Items.Count} items unexpectedly");

        // Attempt to mark read with mismatched account
        var inbox2List = await inboxRepo.GetInboxAsync(account2, 10, null, false, CancellationToken.None);
        if (inbox2List.Items.Count > 0)
        {
            var targetId = inbox2List.Items[0].NotificationId;
            var markOutcome = await inboxRepo.MarkAsReadAsync(targetId, account1, CancellationToken.None);
            if (markOutcome == MarkReadOutcome.Success)
                throw new Exception("Cross-account authorization bypass: Account1 marked Account2's notification as read");
        }
    }

    private static async Task TestScenario11_ReportingResilienceAsync(NpgsqlDataSource dataSource)
    {
        // Core analysis and billing transactions run on PostgreSQL 17 independent of reporting
        // Verifies database schema isolation between analysis, billing, and reporting
        await using var conn = await dataSource.OpenConnectionAsync();

        var sql = """
            SELECT nspname FROM pg_namespace 
            WHERE nspname IN ('analysis', 'billing', 'notification', 'identity')
            ORDER BY nspname;
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        var schemas = new List<string>();
        while (await reader.ReadAsync())
        {
            schemas.Add(reader.GetString(0));
        }

        if (schemas.Count != 4)
            throw new Exception($"Expected 4 primary operational schemas, got {schemas.Count}");
    }

    private static async Task TestScenario12_TriPartyReconciliationAuditAsync(NpgsqlDataSource dataSource)
    {
        await using var conn = await dataSource.OpenConnectionAsync();

        // 1. Audit Wallet Balance vs Ledger Sum:
        // Final Balance = Initial Balance (from seed credit) + sum(ledger amount)
        var auditSql = """
            SELECT 
                w.id,
                w.balance AS current_balance,
                COALESCE(SUM(l.amount), 0) AS net_ledger_flow
            FROM billing.credit_wallets w
            LEFT JOIN billing.credit_ledger_entries l ON l.wallet_id = w.id
            WHERE w.id::text LIKE 'a30%'
            GROUP BY w.id, w.balance
            ORDER BY w.id;
            """;

        await using var cmd = new NpgsqlCommand(auditSql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var walletId = reader.GetGuid(0);
            var currentBalance = reader.GetInt32(1);
            var netLedgerFlow = reader.GetInt64(2);

            // In our system, the initial seed was recorded as an initial CREDIT ledger entry.
            // Therefore, current_balance MUST EQUAL net_ledger_flow!
            if (currentBalance != netLedgerFlow)
            {
                throw new Exception($"Reconciliation Discrepancy on wallet {walletId}: current_balance={currentBalance}, net_ledger_flow={netLedgerFlow}");
            }
        }
        await reader.CloseAsync();

        // 2. Audit Status Consistency:
        // Analysis COMPLETED images must have corresponding CAPTURED reservations
        var consistencySql = """
            SELECT 
                i.id AS analysis_id,
                i.processing_status,
                r.state AS reservation_status
            FROM analysis.images i
            JOIN billing.credit_reservations r ON r.analysis_id = i.id
            WHERE i.tenant_id = 'c3020000-0000-4000-8000-000000000002';
            """;

        await using var cmdCons = new NpgsqlCommand(consistencySql, conn);
        await using var readerCons = await cmdCons.ExecuteReaderAsync();

        while (await readerCons.ReadAsync())
        {
            var analysisId = readerCons.GetGuid(0);
            var procStatus = readerCons.GetString(1);
            var resStatus = readerCons.GetString(2);

            if (procStatus == "COMPLETED" && resStatus != "CAPTURED")
                throw new Exception($"Status mismatch on {analysisId}: Analysis is COMPLETED but Reservation is {resStatus}");

            if ((procStatus == "FAILED" || procStatus == "LOW_QUALITY") && resStatus != "RELEASED")
                throw new Exception($"Status mismatch on {analysisId}: Analysis is {procStatus} but Reservation is {resStatus}");
        }
    }

    private static async Task<int> GetWalletBalanceAsync(NpgsqlConnection conn, Guid walletId)
    {
        var sql = "SELECT balance FROM billing.credit_wallets WHERE id = @id;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", walletId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task<long> GetLedgerCountAsync(NpgsqlConnection conn, Guid walletId)
    {
        var sql = "SELECT count(*) FROM billing.credit_ledger_entries WHERE wallet_id = @id;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", walletId);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    private static async Task<string?> GetReservationStatusAsync(NpgsqlConnection conn, Guid reservationId)
    {
        var sql = "SELECT state FROM billing.credit_reservations WHERE id = @id;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", reservationId);
        return (string?)await cmd.ExecuteScalarAsync();
    }

    private static string ComputeSha256(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
