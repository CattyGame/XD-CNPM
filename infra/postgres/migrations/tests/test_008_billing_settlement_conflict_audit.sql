-- SCRUM-326 regression test: payload conflict must preserve previous settlement evidence.
-- Run after 006_billing_settlement_inbox.sql with psql -v ON_ERROR_STOP=1.
BEGIN;

SET LOCAL ROLE aura_billing_migrator;

DO $$
DECLARE
    test_consumer constant text := 'billing-conflict-audit-test';
    test_event constant uuid := 'a3260000-0000-4000-8000-000000000101';
    test_tenant constant uuid := 'a3260000-0000-4000-8000-000000000102';
    test_analysis constant uuid := 'a3260000-0000-4000-8000-000000000103';
    test_reservation constant uuid := 'a3260000-0000-4000-8000-000000000104';
BEGIN
    INSERT INTO billing.settlement_inbox (
        consumer_name, event_id, event_type, schema_version, occurred_at,
        tenant_id, analysis_id, correlation_id, payload_sha256
    ) VALUES (
        test_consumer, test_event, 'analysis.completed', '0.7', now(),
        test_tenant, test_analysis, 'scrum326-conflict-audit', repeat('a', 64)
    );

    UPDATE billing.settlement_inbox
       SET status = 'COMPLETED',
           next_attempt_at = NULL,
           resolved_reservation_id = test_reservation,
           resolved_generation = 1,
           result_version = 1,
           is_mock = false,
           processing_status = 'COMPLETED',
           settlement_action = 'CAPTURE',
           settlement_reason = 'VALID_RESULT',
           completed_at = now(),
           updated_at = now()
     WHERE consumer_name = test_consumer
       AND event_id = test_event;

    -- Simulate a different payload with the same eventId.
    UPDATE billing.settlement_inbox
       SET conflict_payload_sha256 = COALESCE(conflict_payload_sha256, repeat('b', 64)),
           conflict_prior_status = COALESCE(conflict_prior_status, status),
           conflict_prior_settlement_action = COALESCE(conflict_prior_settlement_action, settlement_action),
           conflict_prior_settlement_reason = COALESCE(conflict_prior_settlement_reason, settlement_reason),
           conflict_prior_completed_at = COALESCE(conflict_prior_completed_at, completed_at),
           status = 'HOLD',
           next_attempt_at = NULL,
           lease_token = NULL,
           lease_until = NULL,
           last_error_code = 'PAYLOAD_CONFLICT',
           settlement_action = NULL,
           settlement_reason = NULL,
           completed_at = NULL,
           updated_at = now()
     WHERE consumer_name = test_consumer
       AND event_id = test_event;

    IF NOT EXISTS (
        SELECT 1
          FROM billing.settlement_inbox
         WHERE consumer_name = test_consumer
           AND event_id = test_event
           AND status = 'HOLD'
           AND payload_sha256 = repeat('a', 64)
           AND conflict_payload_sha256 = repeat('b', 64)
           AND conflict_prior_status = 'COMPLETED'
           AND conflict_prior_settlement_action = 'CAPTURE'
           AND conflict_prior_settlement_reason = 'VALID_RESULT'
           AND conflict_prior_completed_at IS NOT NULL
           AND resolved_reservation_id = test_reservation
           AND resolved_generation = 1
           AND result_version = 1
           AND is_mock = false
           AND processing_status = 'COMPLETED'
           AND settlement_action IS NULL
           AND settlement_reason IS NULL
           AND completed_at IS NULL
    ) THEN
        RAISE EXCEPTION 'Payload conflict did not preserve the prior CAPTURE evidence.';
    END IF;
END
$$;

ROLLBACK;