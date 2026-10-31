-- Transactional tests for the SCRUM-326 Billing settlement inbox.
-- All rows are rolled back; run with psql -v ON_ERROR_STOP=1 against aura_dev.
BEGIN;

-- Runtime role may insert only the immutable event envelope and may update only lifecycle fields.
DO $$
BEGIN
    IF has_table_privilege('aura_billing_app', 'billing.settlement_inbox', 'INSERT') THEN
        RAISE EXCEPTION 'Runtime role must not have table-wide INSERT on settlement inbox.';
    END IF;
    IF has_table_privilege('aura_billing_app', 'billing.settlement_inbox', 'UPDATE') THEN
        RAISE EXCEPTION 'Runtime role must not have table-wide UPDATE on settlement inbox.';
    END IF;
    IF has_table_privilege('aura_billing_app', 'billing.settlement_inbox', 'DELETE') THEN
        RAISE EXCEPTION 'Runtime role must not have DELETE on settlement inbox.';
    END IF;
    IF NOT has_column_privilege('aura_billing_app', 'billing.settlement_inbox', 'event_id', 'INSERT') THEN
        RAISE EXCEPTION 'Runtime role is missing INSERT on event_id.';
    END IF;
    IF has_column_privilege('aura_billing_app', 'billing.settlement_inbox', 'status', 'INSERT') THEN
        RAISE EXCEPTION 'Runtime role must not INSERT settlement status directly.';
    END IF;
    IF NOT has_column_privilege('aura_billing_app', 'billing.settlement_inbox', 'status', 'UPDATE') THEN
        RAISE EXCEPTION 'Runtime role is missing lifecycle UPDATE on status.';
    END IF;
    IF has_column_privilege('aura_billing_app', 'billing.settlement_inbox', 'payload_sha256', 'UPDATE') THEN
        RAISE EXCEPTION 'Runtime role must not UPDATE the original payload fingerprint.';
    END IF;
END
$$;

SET LOCAL ROLE aura_billing_migrator;

DO $$
DECLARE
    test_consumer constant text := 'billing-settlement-v1';
    test_event constant uuid := 'a3260000-0000-4000-8000-000000000001';
    test_tenant constant uuid := 'a3260000-0000-4000-8000-000000000002';
    test_analysis constant uuid := 'a3260000-0000-4000-8000-000000000003';
    test_reservation constant uuid := 'a3260000-0000-4000-8000-000000000004';
    test_lease constant uuid := 'a3260000-0000-4000-8000-000000000005';
    row_count integer;
    current_status text;
    current_attempts smallint;
BEGIN
    INSERT INTO billing.settlement_inbox (
        consumer_name, event_id, event_type, schema_version, occurred_at,
        tenant_id, analysis_id, correlation_id, payload_sha256
    ) VALUES (
        test_consumer, test_event, 'analysis.completed', '0.7', now(),
        test_tenant, test_analysis, 'scrum326-test-1', repeat('a', 64)
    );

    -- Same consumer/eventId is idempotent: a duplicate must not add a row.
    INSERT INTO billing.settlement_inbox (
        consumer_name, event_id, event_type, schema_version, occurred_at,
        tenant_id, analysis_id, correlation_id, payload_sha256
    ) VALUES (
        test_consumer, test_event, 'analysis.completed', '0.7', now(),
        test_tenant, test_analysis, 'scrum326-test-1', repeat('a', 64)
    ) ON CONFLICT (consumer_name, event_id) DO NOTHING;

    SELECT count(*) INTO row_count
    FROM billing.settlement_inbox
    WHERE consumer_name = test_consumer AND event_id = test_event;
    IF row_count <> 1 THEN
        RAISE EXCEPTION 'Duplicate event created more than one inbox row: %', row_count;
    END IF;

    -- A due row can be leased and a resolver attempt durably counted.
    UPDATE billing.settlement_inbox
       SET resolver_attempts = 1,
           lease_token = test_lease,
           lease_until = now() + interval '30 seconds',
           next_attempt_at = now() + interval '30 seconds',
           updated_at = now()
     WHERE consumer_name = test_consumer AND event_id = test_event;

    SELECT status, resolver_attempts INTO current_status, current_attempts
    FROM billing.settlement_inbox
    WHERE consumer_name = test_consumer AND event_id = test_event;
    IF current_status <> 'PENDING' OR current_attempts <> 1 THEN
        RAISE EXCEPTION 'Pending lease/attempt invariant failed: status=%, attempts=%',
            current_status, current_attempts;
    END IF;

    -- After an authoritative CAPTURE, the inbox becomes terminal and clears retry/lease state.
    UPDATE billing.settlement_inbox
       SET status = 'COMPLETED',
           resolver_attempts = 1,
           next_attempt_at = NULL,
           lease_token = NULL,
           lease_until = NULL,
           resolved_reservation_id = test_reservation,
           resolved_generation = 1,
           result_version = 1,
           is_mock = false,
           processing_status = 'COMPLETED',
           settlement_action = 'CAPTURE',
           settlement_reason = 'VALID_RESULT',
           completed_at = now(),
           updated_at = now()
     WHERE consumer_name = test_consumer AND event_id = test_event;

    SELECT status INTO current_status
    FROM billing.settlement_inbox
    WHERE consumer_name = test_consumer AND event_id = test_event;
    IF current_status <> 'COMPLETED' THEN
        RAISE EXCEPTION 'Expected terminal COMPLETED inbox state; found %', current_status;
    END IF;

    -- Same eventId with a different body must retain the original hash and move to HOLD.
    INSERT INTO billing.settlement_inbox (
        consumer_name, event_id, event_type, schema_version, occurred_at,
        tenant_id, analysis_id, correlation_id, payload_sha256
    ) VALUES (
        test_consumer, 'a3260000-0000-4000-8000-000000000006',
        'analysis.failed', '0.7', now(), test_tenant, test_analysis,
        'scrum326-test-2', repeat('b', 64)
    );

    INSERT INTO billing.settlement_inbox (
        consumer_name, event_id, event_type, schema_version, occurred_at,
        tenant_id, analysis_id, correlation_id, payload_sha256
    ) VALUES (
        test_consumer, 'a3260000-0000-4000-8000-000000000006',
        'analysis.failed', '0.7', now(), test_tenant, test_analysis,
        'scrum326-test-2', repeat('c', 64)
    )
    ON CONFLICT (consumer_name, event_id) DO UPDATE
       SET conflict_payload_sha256 = EXCLUDED.payload_sha256,
           status = 'HOLD',
           next_attempt_at = NULL,
           lease_token = NULL,
           lease_until = NULL,
           settlement_action = NULL,
           settlement_reason = NULL,
           completed_at = NULL,
           last_error_code = 'EVENT_ID_PAYLOAD_MISMATCH',
           updated_at = now();

    SELECT status INTO current_status
    FROM billing.settlement_inbox
    WHERE consumer_name = test_consumer
      AND event_id = 'a3260000-0000-4000-8000-000000000006';
    IF current_status <> 'HOLD' THEN
        RAISE EXCEPTION 'Conflicting event payload was not held.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM billing.settlement_inbox
        WHERE consumer_name = test_consumer
          AND event_id = 'a3260000-0000-4000-8000-000000000006'
          AND payload_sha256 = repeat('b', 64)
          AND conflict_payload_sha256 = repeat('c', 64)
          AND last_error_code = 'EVENT_ID_PAYLOAD_MISMATCH'
    ) THEN
        RAISE EXCEPTION 'Original/conflicting payload fingerprints were not preserved.';
    END IF;

    -- RELEASE must fail closed for mock/unknown Analysis state.
    INSERT INTO billing.settlement_inbox (
        consumer_name, event_id, event_type, schema_version, occurred_at,
        tenant_id, analysis_id, correlation_id, payload_sha256
    ) VALUES (
        test_consumer, 'a3260000-0000-4000-8000-000000000008',
        'analysis.failed', '0.7', now(), test_tenant, test_analysis,
        'scrum326-release-mock', repeat('d', 64)
    );

    BEGIN
        UPDATE billing.settlement_inbox
           SET status = 'COMPLETED',
               next_attempt_at = NULL,
               resolved_reservation_id = test_reservation,
               resolved_generation = 1,
               is_mock = true,
               processing_status = 'FAILED',
               settlement_action = 'RELEASE',
               settlement_reason = 'FINAL_FAILURE',
               completed_at = now(),
               updated_at = now()
         WHERE consumer_name = test_consumer
           AND event_id = 'a3260000-0000-4000-8000-000000000008';
        RAISE EXCEPTION 'Expected mock RELEASE to be rejected by the database constraint.';
    EXCEPTION WHEN check_violation THEN
        NULL;
    END;

    -- A failed status with a persisted result version is also not releasable.
    INSERT INTO billing.settlement_inbox (
        consumer_name, event_id, event_type, schema_version, occurred_at,
        tenant_id, analysis_id, correlation_id, payload_sha256
    ) VALUES (
        test_consumer, 'a3260000-0000-4000-8000-000000000009',
        'analysis.failed', '0.7', now(), test_tenant, test_analysis,
        'scrum326-release-result', repeat('e', 64)
    );

    BEGIN
        UPDATE billing.settlement_inbox
           SET status = 'COMPLETED',
               next_attempt_at = NULL,
               resolved_reservation_id = test_reservation,
               resolved_generation = 1,
               result_version = 1,
               is_mock = false,
               processing_status = 'FAILED',
               settlement_action = 'RELEASE',
               settlement_reason = 'FINAL_FAILURE',
               completed_at = now(),
               updated_at = now()
         WHERE consumer_name = test_consumer
           AND event_id = 'a3260000-0000-4000-8000-000000000009';
        RAISE EXCEPTION 'Expected RELEASE with a persisted result version to be rejected.';
    EXCEPTION WHEN check_violation THEN
        NULL;
    END;

    -- Invalid fingerprints must be rejected by the database constraint.
    BEGIN
        INSERT INTO billing.settlement_inbox (
            consumer_name, event_id, event_type, schema_version, occurred_at,
            tenant_id, analysis_id, correlation_id, payload_sha256
        ) VALUES (
            test_consumer, 'a3260000-0000-4000-8000-000000000007',
            'analysis.failed', '0.7', now(), test_tenant, test_analysis,
            'scrum326-test-3', 'not-a-sha256'
        );
        RAISE EXCEPTION 'Expected invalid payload SHA-256 check to reject the row.';
    EXCEPTION WHEN check_violation THEN
        NULL;
    END;

    RAISE NOTICE 'PASS: settlement inbox idempotency, durable lease, terminal state, payload conflict HOLD and fingerprint checks.';
END
$$;

ROLLBACK;