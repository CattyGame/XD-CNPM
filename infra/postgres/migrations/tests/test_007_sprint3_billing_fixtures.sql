-- ====================================================================
-- AURA Sprint 3 — Billing Test Fixtures (SCRUM-326 / W3-M3-01)
-- Isolated TEST wallets for concurrency, idempotency, lifecycle and reconciliation.
-- Does NOT modify, credit or touch any fixture 08 wallet.
-- ====================================================================

BEGIN;

-- 0. Identity Accounts for Sprint 3 Fixture Actors
INSERT INTO identity.accounts (account_id, firebase_uid, account_status, roles, profile_provisioning_status, created_at, updated_at)
VALUES 
    ('e3010000-0000-4000-8000-000000000001', 'fb_uid_e301_sprint3', 'ACTIVE', '["PATIENT"]'::jsonb, 'COMPLETED', NOW(), NOW()),
    ('e3020000-0000-4000-8000-000000000002', 'fb_uid_e302_sprint3', 'ACTIVE', '["PATIENT"]'::jsonb, 'COMPLETED', NOW(), NOW()),
    ('e3030000-0000-4000-8000-000000000003', 'fb_uid_e303_sprint3', 'ACTIVE', '["PATIENT"]'::jsonb, 'COMPLETED', NOW(), NOW()),
    ('e3040000-0000-4000-8000-000000000004', 'fb_uid_e304_sprint3', 'ACTIVE', '["CLINIC_ADMIN"]'::jsonb, 'COMPLETED', NOW(), NOW()),
    ('e3050000-0000-4000-8000-000000000005', 'fb_uid_e305_sprint3', 'ACTIVE', '["PATIENT"]'::jsonb, 'COMPLETED', NOW(), NOW())
ON CONFLICT (account_id) DO UPDATE
SET account_status = 'ACTIVE', updated_at = NOW();

-- 1. Single-Credit TEST Wallet (Used for 2-request concurrency race condition test)
-- Owner: Account e3010000-0000-4000-8000-000000000001
INSERT INTO billing.credit_wallets (id, owner_type, owner_id, environment, balance, created_at, updated_at)
VALUES (
    'a3010000-0000-4000-8000-000000000001',
    'ACCOUNT',
    'e3010000-0000-4000-8000-000000000001',
    'TEST',
    1,
    NOW(),
    NOW()
)
ON CONFLICT (owner_type, owner_id, environment) DO UPDATE
SET balance = 1, updated_at = NOW();

-- Initial CREDIT ledger entry for audit trail
INSERT INTO billing.credit_ledger_entries (
    id, wallet_id, tenant_id, order_id, reservation_id, entry_type,
    amount, balance_after, idempotency_key, request_fingerprint, metadata, created_at
)
VALUES (
    'b3010000-0000-4000-8000-000000000001',
    'a3010000-0000-4000-8000-000000000001',
    'c3010000-0000-4000-8000-000000000001',
    NULL,
    NULL,
    'CREDIT',
    1,
    1,
    'f3010000-0000-4000-8000-000000000001',
    'sprint3-fixture-seed-single-credit',
    '{"purpose": "sprint3_concurrency_fixture"}'::jsonb,
    NOW()
)
ON CONFLICT (tenant_id, idempotency_key) DO NOTHING;


-- 2. Multi-Credit TEST Wallet (10 credits, used for normal reserve, capture, release lifecycle)
-- Owner: Account e3020000-0000-4000-8000-000000000002
INSERT INTO billing.credit_wallets (id, owner_type, owner_id, environment, balance, created_at, updated_at)
VALUES (
    'a3020000-0000-4000-8000-000000000002',
    'ACCOUNT',
    'e3020000-0000-4000-8000-000000000002',
    'TEST',
    10,
    NOW(),
    NOW()
)
ON CONFLICT (owner_type, owner_id, environment) DO UPDATE
SET balance = 10, updated_at = NOW();

INSERT INTO billing.credit_ledger_entries (
    id, wallet_id, tenant_id, order_id, reservation_id, entry_type,
    amount, balance_after, idempotency_key, request_fingerprint, metadata, created_at
)
VALUES (
    'b3020000-0000-4000-8000-000000000002',
    'a3020000-0000-4000-8000-000000000002',
    'c3020000-0000-4000-8000-000000000002',
    NULL,
    NULL,
    'CREDIT',
    10,
    10,
    'f3020000-0000-4000-8000-000000000002',
    'sprint3-fixture-seed-multi-credit',
    '{"purpose": "sprint3_lifecycle_fixture"}'::jsonb,
    NOW()
)
ON CONFLICT (tenant_id, idempotency_key) DO NOTHING;


-- 3. Zero-Credit TEST Wallet (0 credits, used for 402 Insufficient Credit testing)
-- Owner: Account e3030000-0000-4000-8000-000000000003
INSERT INTO billing.credit_wallets (id, owner_type, owner_id, environment, balance, created_at, updated_at)
VALUES (
    'a3030000-0000-4000-8000-000000000003',
    'ACCOUNT',
    'e3030000-0000-4000-8000-000000000003',
    'TEST',
    0,
    NOW(),
    NOW()
)
ON CONFLICT (owner_type, owner_id, environment) DO UPDATE
SET balance = 0, updated_at = NOW();


-- 4. Clinic Multi-Credit TEST Wallet (5 credits, used for clinic billing owner)
-- Owner: Clinic e3040000-0000-4000-8000-000000000004
INSERT INTO billing.credit_wallets (id, owner_type, owner_id, environment, balance, created_at, updated_at)
VALUES (
    'a3040000-0000-4000-8000-000000000004',
    'CLINIC',
    'e3040000-0000-4000-8000-000000000004',
    'TEST',
    5,
    NOW(),
    NOW()
)
ON CONFLICT (owner_type, owner_id, environment) DO UPDATE
SET balance = 5, updated_at = NOW();

INSERT INTO billing.credit_ledger_entries (
    id, wallet_id, tenant_id, order_id, reservation_id, entry_type,
    amount, balance_after, idempotency_key, request_fingerprint, metadata, created_at
)
VALUES (
    'b3040000-0000-4000-8000-000000000004',
    'a3040000-0000-4000-8000-000000000004',
    'c3040000-0000-4000-8000-000000000004',
    NULL,
    NULL,
    'CREDIT',
    5,
    5,
    'f3040000-0000-4000-8000-000000000004',
    'sprint3-fixture-seed-clinic-credit',
    '{"purpose": "sprint3_clinic_fixture"}'::jsonb,
    NOW()
)
ON CONFLICT (tenant_id, idempotency_key) DO NOTHING;


-- 5. LIVE Wallet (Used to test that mock results are strictly forbidden from capturing on LIVE wallets)
-- Owner: Account e3050000-0000-4000-8000-000000000005
INSERT INTO billing.credit_wallets (id, owner_type, owner_id, environment, balance, created_at, updated_at)
VALUES (
    'a3050000-0000-4000-8000-000000000005',
    'ACCOUNT',
    'e3050000-0000-4000-8000-000000000005',
    'LIVE',
    5,
    NOW(),
    NOW()
)
ON CONFLICT (owner_type, owner_id, environment) DO UPDATE
SET balance = 5, updated_at = NOW();

INSERT INTO billing.credit_ledger_entries (
    id, wallet_id, tenant_id, order_id, reservation_id, entry_type,
    amount, balance_after, idempotency_key, request_fingerprint, metadata, created_at
)
VALUES (
    'b3050000-0000-4000-8000-000000000005',
    'a3050000-0000-4000-8000-000000000005',
    'c3050000-0000-4000-8000-000000000005',
    NULL,
    NULL,
    'CREDIT',
    5,
    5,
    'f3050000-0000-4000-8000-000000000005',
    'sprint3-fixture-seed-live-credit',
    '{"purpose": "sprint3_live_boundary_fixture"}'::jsonb,
    NOW()
)
ON CONFLICT (tenant_id, idempotency_key) DO NOTHING;

COMMIT;
