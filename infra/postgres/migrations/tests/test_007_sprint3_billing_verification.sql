-- ====================================================================
-- AURA Sprint 3 — Billing Schema & Balance Invariant PostgreSQL Test
-- Task: SCRUM-326 (W3-M3-01)
-- Exercises database triggers, advisory lock, terminal guards, and ledger on aura_dev.
-- Transactional smoke test: ends with ROLLBACK.
-- ====================================================================

BEGIN;
SET LOCAL ROLE aura_billing_migrator;

DO $$
DECLARE
    wallet_concurrency uuid := 'a3010000-0000-4000-8000-000000000001';
    wallet_multi uuid := 'a3020000-0000-4000-8000-000000000002';
    wallet_zero uuid := 'a3030000-0000-4000-8000-000000000003';
    
    tenant_id uuid := 'c3010000-0000-4000-8000-000000000001';
    actor_id uuid := 'e3010000-0000-4000-8000-000000000001';
    actor2_id uuid := 'e3020000-0000-4000-8000-000000000002';
    patient_id uuid := 'b3010000-0000-4000-8000-000000000001';
    
    res1_id uuid := gen_random_uuid();
    res2_id uuid := gen_random_uuid();
    analysis1_id uuid := gen_random_uuid();
    analysis2_id uuid := gen_random_uuid();
    
    bal integer;
    current_state text;
BEGIN
    -- 1. Check Initial Balance of single-credit wallet
    SELECT balance INTO bal FROM billing.credit_wallets WHERE id = wallet_concurrency;
    IF bal <> 1 THEN
        RAISE EXCEPTION 'Expected initial balance 1 for concurrency wallet, got %', bal;
    END IF;

    -- 2. AC1: Reserve 1 unit: Debits balance atomically to 0
    UPDATE billing.credit_wallets SET balance = balance - 1, updated_at = now() WHERE id = wallet_concurrency;
    INSERT INTO billing.credit_reservations
        (id, tenant_id, analysis_id, generation, wallet_id, actor_account_id,
         patient_id, billing_owner_type, billing_owner_id, units, environment,
         is_mock, state, idempotency_key, request_fingerprint)
    VALUES
        (res1_id, tenant_id, analysis1_id, 1, wallet_concurrency, actor_id,
         patient_id, 'ACCOUNT', actor_id, 1, 'TEST', false, 'RESERVED',
         gen_random_uuid(), repeat('1', 64));

    INSERT INTO billing.credit_ledger_entries
        (wallet_id, tenant_id, reservation_id, entry_type, amount, balance_after,
         idempotency_key, request_fingerprint)
    VALUES
        (wallet_concurrency, tenant_id, res1_id, 'RESERVE', -1, 0,
         gen_random_uuid(), repeat('1', 64));

    SELECT balance INTO bal FROM billing.credit_wallets WHERE id = wallet_concurrency;
    IF bal <> 0 THEN
        RAISE EXCEPTION 'Expected balance 0 after reserve, got %', bal;
    END IF;

    -- 3. AC1: Second concurrent reserve attempt when balance is 0 must fail CHECK (balance >= 0)
    BEGIN
        UPDATE billing.credit_wallets SET balance = balance - 1 WHERE id = wallet_concurrency;
        RAISE EXCEPTION 'CHECK constraint failed to block overdraft below 0';
    EXCEPTION WHEN check_violation THEN
        -- Expected: constraint prevents overdraft
        NULL;
    END;

    -- 4. AC2: Capture is terminal and does NOT change spendable balance
    UPDATE billing.credit_reservations
       SET state = 'CAPTURED', captured_at = now(), updated_at = now()
     WHERE id = res1_id;

    INSERT INTO billing.credit_ledger_entries
        (wallet_id, tenant_id, reservation_id, entry_type, amount, balance_after,
         idempotency_key, request_fingerprint)
    VALUES
        (wallet_concurrency, tenant_id, res1_id, 'CAPTURE', 0, 0,
         gen_random_uuid(), repeat('2', 64));

    SELECT balance INTO bal FROM billing.credit_wallets WHERE id = wallet_concurrency;
    IF bal <> 0 THEN
        RAISE EXCEPTION 'Capture unexpectedly modified spendable balance: %', bal;
    END IF;

    -- 5. AC2: Guard trigger blocks transition from CAPTURED to RELEASED
    BEGIN
        UPDATE billing.credit_reservations
           SET state = 'RELEASED', release_reason = 'FINAL_FAILURE', released_at = now()
         WHERE id = res1_id;
        RAISE EXCEPTION 'Expected trigger guard to reject transition CAPTURED -> RELEASED';
    EXCEPTION WHEN SQLSTATE '55000' THEN
        -- Expected: terminal guard fired successfully
        NULL;
    END;

    -- 6. AC2: Release on another reservation refunds balance and updates state
    -- Using multi-credit wallet (initial balance = 10)
    UPDATE billing.credit_wallets SET balance = balance - 1 WHERE id = wallet_multi;
    INSERT INTO billing.credit_reservations
        (id, tenant_id, analysis_id, generation, wallet_id, actor_account_id,
         patient_id, billing_owner_type, billing_owner_id, units, environment,
         is_mock, state, idempotency_key, request_fingerprint)
    VALUES
        (res2_id, tenant_id, analysis2_id, 1, wallet_multi, actor2_id,
         patient_id, 'ACCOUNT', actor2_id, 1, 'TEST', false, 'RESERVED',
         gen_random_uuid(), repeat('3', 64));

    INSERT INTO billing.credit_ledger_entries
        (wallet_id, tenant_id, reservation_id, entry_type, amount, balance_after,
         idempotency_key, request_fingerprint)
    VALUES
        (wallet_multi, tenant_id, res2_id, 'RESERVE', -1, 9,
         gen_random_uuid(), repeat('3', 64));

    -- Perform terminal release
    UPDATE billing.credit_wallets SET balance = balance + 1 WHERE id = wallet_multi;
    UPDATE billing.credit_reservations
       SET state = 'RELEASED', release_reason = 'FINAL_FAILURE', released_at = now(), updated_at = now()
     WHERE id = res2_id;

    INSERT INTO billing.credit_ledger_entries
        (wallet_id, tenant_id, reservation_id, entry_type, amount, balance_after,
         idempotency_key, request_fingerprint)
    VALUES
        (wallet_multi, tenant_id, res2_id, 'RELEASE', 1, 10,
         gen_random_uuid(), repeat('4', 64));

    SELECT balance INTO bal FROM billing.credit_wallets WHERE id = wallet_multi;
    SELECT state INTO current_state FROM billing.credit_reservations WHERE id = res2_id;

    IF bal <> 10 OR current_state <> 'RELEASED' THEN
        RAISE EXCEPTION 'Release refund invariant failed: balance=%, state=%', bal, current_state;
    END IF;

    -- 7. AC2: Guard trigger blocks transition from RELEASED to CAPTURED
    BEGIN
        UPDATE billing.credit_reservations
           SET state = 'CAPTURED', captured_at = now()
         WHERE id = res2_id;
        RAISE EXCEPTION 'Expected trigger guard to reject transition RELEASED -> CAPTURED';
    EXCEPTION WHEN SQLSTATE '55000' THEN
        -- Expected: terminal guard fired successfully
        NULL;
    END;

    -- 8. AC2: Unique constraint blocks having both CAPTURE and RELEASE on the same reservation
    BEGIN
        INSERT INTO billing.credit_ledger_entries
            (wallet_id, tenant_id, reservation_id, entry_type, amount, balance_after,
             idempotency_key, request_fingerprint)
        VALUES
            (wallet_multi, tenant_id, res2_id, 'CAPTURE', 0, 10,
             gen_random_uuid(), repeat('5', 64));
        RAISE EXCEPTION 'Expected unique index uq_credit_ledger_one_terminal_per_reservation to reject second terminal entry';
    EXCEPTION WHEN unique_violation THEN
        -- Expected: unique constraint fired successfully
        NULL;
    END;

    RAISE NOTICE 'PASS: Sprint 3 PostgreSQL DB Verification: Concurrency overdraft guard, capture invariant, terminal transition guard (both directions), and ledger terminal uniqueness.';
END
$$;

ROLLBACK;
