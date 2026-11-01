BEGIN;

DO $$
BEGIN
    IF current_database() <> 'aura_dev' THEN
        RAISE EXCEPTION 'Run this migration only on aura_dev';
    END IF;
END
$$;

-- SCRUM-346: Billing Payments and Active Packages Catalog
SET LOCAL ROLE aura_billing_migrator;

-- 1. Sync legacy billing_poc.service_packages with official Sprint 4 Packages
UPDATE billing_poc.service_packages
SET name = 'Gói cá nhân lẻ', price = 20000.00, credits = 1, updated_at = now()
WHERE id = 1;

UPDATE billing_poc.service_packages
SET name = 'Gói gia đình', price = 90000.00, credits = 5, updated_at = now()
WHERE id = 2;

UPDATE billing_poc.service_packages
SET name = 'Gói tầm soát tổ chức', price = 1500000.00, credits = 100, updated_at = now()
WHERE id = 3;

-- 2. Insert ACTIVE package price versions for orders snapshot validation
INSERT INTO billing.package_price_versions
    (package_id, version_number, display_name, price_amount, currency_code, credits, status, effective_from)
VALUES
    (1, 2, 'Gói cá nhân lẻ', 20000.00, 'VND', 1, 'ACTIVE', now()),
    (2, 2, 'Gói gia đình', 90000.00, 'VND', 5, 'ACTIVE', now()),
    (3, 2, 'Gói tầm soát tổ chức', 1500000.00, 'VND', 100, 'ACTIVE', now())
ON CONFLICT (package_id, version_number) DO NOTHING;

-- 3. Create billing.payments table for payment tracking and audit
CREATE TABLE IF NOT EXISTS billing.payments (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id uuid NOT NULL REFERENCES billing.orders(id),
    payment_method varchar(32) NOT NULL,
    transaction_id varchar(128) NOT NULL,
    amount numeric(12, 2) NOT NULL CHECK (amount >= 0),
    currency varchar(3) NOT NULL,
    status varchar(16) NOT NULL CHECK (status IN ('PENDING', 'SUCCESS', 'FAILED')),
    signature text NOT NULL,
    raw_payload jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_billing_payment_order_tx UNIQUE (order_id, transaction_id)
);

CREATE INDEX IF NOT EXISTS ix_billing_payments_order_id ON billing.payments(order_id);
CREATE INDEX IF NOT EXISTS ix_billing_payments_transaction_id ON billing.payments(transaction_id);

-- 4. Table grants
GRANT SELECT, INSERT, UPDATE ON billing.payments TO aura_billing_app;

COMMIT;
