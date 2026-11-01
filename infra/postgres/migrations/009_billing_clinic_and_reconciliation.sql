BEGIN;

DO $$
BEGIN
    IF current_database() <> 'aura_dev' THEN
        RAISE EXCEPTION 'Run this migration only on aura_dev';
    END IF;
END
$$;

-- SCRUM-348: Clinic Billing Packages & Reconciliation Release Reasons
SET LOCAL ROLE aura_billing_migrator;

-- 1. Extend release reasons to support automatic reconciliation and batch refund
ALTER TABLE billing.credit_reservations DROP CONSTRAINT IF EXISTS credit_reservations_release_reason_check;
ALTER TABLE billing.credit_reservations ADD CONSTRAINT credit_reservations_release_reason_check
    CHECK (release_reason IS NULL OR release_reason IN (
        'VALIDATION_ABORT', 'STORAGE_ABORT', 'ANALYSIS_ABORT',
        'FINAL_FAILURE', 'LOW_QUALITY', 'STALE_RESERVATION_AUTO_REFUND', 'BATCH_PARTIAL_REFUND'
    ));

-- 2. Add 500-credit Clinic Enterprise Package
INSERT INTO billing_poc.service_packages (id, name, price, credits, updated_at)
VALUES (4, 'Gói phòng khám Enterprise', 7000000.00, 500, now())
ON CONFLICT (id) DO UPDATE
SET name = EXCLUDED.name, price = EXCLUDED.price, credits = EXCLUDED.credits, updated_at = now();

INSERT INTO billing.package_price_versions
    (package_id, version_number, display_name, price_amount, currency_code, credits, status, effective_from)
VALUES
    (4, 2, 'Gói phòng khám Enterprise', 7000000.00, 'VND', 500, 'ACTIVE', now())
ON CONFLICT (package_id, version_number) DO NOTHING;

COMMIT;
