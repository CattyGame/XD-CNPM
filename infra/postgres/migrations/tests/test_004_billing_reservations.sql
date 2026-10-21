-- Transactional smoke test for the Billing reservation schema and terminal guard.
-- Run against the disposable/local aura_dev database with psql -v ON_ERROR_STOP=1.
-- All test data is rolled back at the end.
BEGIN;
SET LOCAL ROLE aura_billing_migrator;

DO $$
DECLARE
    test_owner uuid := 'a0010000-0000-4000-8000-000000000001';
    test_tenant uuid := 'c0010000-0000-4000-8000-000000000001';
    test_actor uuid := 'a0010000-0000-4000-8000-000000000001';
    test_patient uuid := 'b0010000-0000-4000-8000-000000000001';
    test_wallet uuid := 'e0010000-0000-4000-8000-000000000001';
    reserve_id uuid := 'e0020000-0000-4000-8000-000000000001';
    capture_id uuid := 'd0010000-0000-4000-8000-000000000001';
    release_id uuid := 'd0020000-0000-4000-8000-000000000001';
    capture_key uuid := 'f0010000-0000-4000-8000-000000000001';
    release_key uuid := 'f0020000-0000-4000-8000-000000000001';
    bal integer;
    current_state text;
BEGIN
    INSERT INTO billing.credit_wallets
        (id, owner_type, owner_id, environment, balance)
    VALUES (test_wallet, 'ACCOUNT', test_owner, 'TEST', 2);

    -- Reserve 1: balance debits atomically and ledger records the negative delta.
    UPDATE billing.credit_wallets SET balance = balance - 1 WHERE id = test_wallet;
    INSERT INTO billing.credit_reservations
        (id, tenant_id, analysis_id, generation, wallet_id, actor_account_id,
         patient_id, billing_owner_type, billing_owner_id, units, environment,
         is_mock, state, idempotency_key, request_fingerprint)
    VALUES
        (reserve_id, test_tenant, capture_id, 1, test_wallet, test_actor,
         test_patient, 'ACCOUNT', test_owner, 1, 'TEST', true, 'RESERVED',
         'f0030000-0000-4000-8000-000000000001', repeat('a', 64));
    INSERT INTO billing.credit_ledger_entries
        (wallet_id, tenant_id, reservation_id, entry_type, amount, balance_after,
         idempotency_key, request_fingerprint)
    VALUES
        (test_wallet, test_tenant, reserve_id, 'RESERVE', -1, 1,
         'f0030000-0000-4000-8000-000000000001', repeat('a', 64));

    -- Capture is terminal and does not refund a credit already debited at reserve.
    UPDATE billing.credit_reservations
       SET state = 'CAPTURED', captured_at = now(), updated_at = now()
     WHERE id = reserve_id;
    INSERT INTO billing.credit_ledger_entries
        (wallet_id, tenant_id, reservation_id, entry_type, amount, balance_after,
         idempotency_key, request_fingerprint)
    VALUES
        (test_wallet, test_tenant, reserve_id, 'CAPTURE', 0, 1,
         capture_key, repeat('b', 64));

    BEGIN
        UPDATE billing.credit_reservations
           SET state = 'RELEASED', release_reason = 'FINAL_FAILURE', released_at = now()
         WHERE id = reserve_id;
        RAISE EXCEPTION 'Expected terminal-state guard to reject CAPTURED -> RELEASED';
    EXCEPTION WHEN SQLSTATE '55000' THEN
        NULL;
    END;

    SELECT balance INTO bal FROM billing.credit_wallets WHERE id = test_wallet;
    IF bal <> 1 THEN
        RAISE EXCEPTION 'Capture changed spendable balance unexpectedly: %', bal;
    END IF;

    -- A separate reservation release restores exactly one credit.
    UPDATE billing.credit_wallets SET balance = balance - 1 WHERE id = test_wallet;
    INSERT INTO billing.credit_reservations
        (id, tenant_id, analysis_id, generation, wallet_id, actor_account_id,
         patient_id, billing_owner_type, billing_owner_id, units, environment,
         is_mock, state, idempotency_key, request_fingerprint)
    VALUES
        (release_id, test_tenant, 'd0030000-0000-4000-8000-000000000001', 1,
         test_wallet, test_actor, test_patient, 'ACCOUNT', test_owner, 1,
         'TEST', true, 'RESERVED', 'f0040000-0000-4000-8000-000000000001', repeat('c', 64));
    INSERT INTO billing.credit_ledger_entries
        (wallet_id, tenant_id, reservation_id, entry_type, amount, balance_after,
         idempotency_key, request_fingerprint)
    VALUES
        (test_wallet, test_tenant, release_id, 'RESERVE', -1, 0,
         'f0040000-0000-4000-8000-000000000001', repeat('c', 64));

    UPDATE billing.credit_wallets SET balance = balance + 1 WHERE id = test_wallet;
    UPDATE billing.credit_reservations
       SET state = 'RELEASED', release_reason = 'FINAL_FAILURE', released_at = now(), updated_at = now()
     WHERE id = release_id;
    INSERT INTO billing.credit_ledger_entries
        (wallet_id, tenant_id, reservation_id, entry_type, amount, balance_after,
         idempotency_key, request_fingerprint)
    VALUES
        (test_wallet, test_tenant, release_id, 'RELEASE', 1, 1,
         release_key, repeat('d', 64));

    SELECT balance INTO bal FROM billing.credit_wallets WHERE id = test_wallet;
    SELECT state INTO current_state FROM billing.credit_reservations WHERE id = release_id;
    IF bal <> 1 OR current_state <> 'RELEASED' THEN
        RAISE EXCEPTION 'Release invariant failed: balance=%, state=%', bal, current_state;
    END IF;

    RAISE NOTICE 'PASS: reserve ledger, capture terminal guard and release balance invariant.';
END
$$;

ROLLBACK;
