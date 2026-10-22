BEGIN;

DO $$
BEGIN
    IF current_database() <> 'aura_dev' THEN
        RAISE EXCEPTION 'Run this migration only on aura_dev';
    END IF;
END
$$;

-- SCRUM-306: Billing business schema v1.
-- The legacy billing_poc catalog remains intact and is not a source of
-- orderable prices. Legacy prices are copied only as DEMO records.
SET LOCAL ROLE aura_billing_migrator;

CREATE TABLE IF NOT EXISTS billing.package_price_versions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    package_id integer NOT NULL,
    version_number integer NOT NULL CHECK (version_number > 0),
    display_name varchar(100) NOT NULL,
    price_amount numeric(12, 2) NOT NULL CHECK (price_amount >= 0),
    currency_code varchar(3),
    credits integer NOT NULL CHECK (credits > 0),
    status varchar(16) NOT NULL DEFAULT 'DEMO'
        CHECK (status IN ('DEMO', 'DRAFT', 'ACTIVE', 'RETIRED')),
    effective_from timestamptz,
    effective_to timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_package_price_version UNIQUE (package_id, version_number),
    CONSTRAINT uq_package_price_version_id_package UNIQUE (id, package_id),
    CONSTRAINT ck_price_version_currency
        CHECK (
            (status = 'DEMO' AND currency_code IS NULL)
            OR
            (status = 'DRAFT'
                AND (currency_code IS NULL OR currency_code ~ '^[A-Z]{3}$'))
            OR
            (status IN ('ACTIVE', 'RETIRED')
                AND currency_code IS NOT NULL
                AND currency_code ~ '^[A-Z]{3}$')
        ),
    CONSTRAINT ck_price_version_effective_window
        CHECK (effective_to IS NULL OR effective_from IS NULL OR effective_to > effective_from)
);

-- Existing PoC prices are retained as demo records. Do not infer currency.
INSERT INTO billing.package_price_versions
    (package_id, version_number, display_name, price_amount, currency_code,
     credits, status)
SELECT id, 1, name, price, NULL, credits, 'DEMO'
FROM billing_poc.service_packages
ON CONFLICT (package_id, version_number) DO NOTHING;

CREATE UNIQUE INDEX IF NOT EXISTS uq_package_one_active_price_version
    ON billing.package_price_versions (package_id)
    WHERE status = 'ACTIVE';

-- Once a price version is published, its financial fields cannot be edited.
-- A draft may only receive its currency and be activated/retired; create a new
-- version for any change to amount, credits, name, package, or effective_from.
CREATE OR REPLACE FUNCTION billing.guard_package_price_version_update()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'Package price versions cannot be deleted'
            USING ERRCODE = '55000';
    END IF;

    IF OLD.status IN ('DEMO', 'RETIRED') THEN
        RAISE EXCEPTION 'DEMO and RETIRED price versions are immutable'
            USING ERRCODE = '55000';
    END IF;

    IF NEW.id IS DISTINCT FROM OLD.id
       OR NEW.package_id IS DISTINCT FROM OLD.package_id
       OR NEW.version_number IS DISTINCT FROM OLD.version_number
       OR NEW.display_name IS DISTINCT FROM OLD.display_name
       OR NEW.price_amount IS DISTINCT FROM OLD.price_amount
       OR NEW.credits IS DISTINCT FROM OLD.credits
       OR NEW.effective_from IS DISTINCT FROM OLD.effective_from
       OR NEW.created_at IS DISTINCT FROM OLD.created_at THEN
        RAISE EXCEPTION 'Price version fields are immutable; create a new version'
            USING ERRCODE = '55000';
    END IF;

    IF OLD.status = 'ACTIVE' THEN
        IF NEW.status NOT IN ('ACTIVE', 'RETIRED')
           OR NEW.currency_code IS DISTINCT FROM OLD.currency_code THEN
            RAISE EXCEPTION 'An ACTIVE price version can only be retired'
                USING ERRCODE = '55000';
        END IF;
        IF NEW.status = 'ACTIVE'
           AND NEW.effective_to IS DISTINCT FROM OLD.effective_to THEN
            RAISE EXCEPTION 'An ACTIVE price version cannot change effective_to'
                USING ERRCODE = '55000';
        END IF;
    ELSIF OLD.status = 'DRAFT' THEN
        IF NEW.status NOT IN ('DRAFT', 'ACTIVE', 'RETIRED') THEN
            RAISE EXCEPTION 'Invalid price version state transition'
                USING ERRCODE = '55000';
        END IF;
    END IF;

    IF NEW.status = 'RETIRED' AND NEW.effective_to IS NULL THEN
        NEW.effective_to := now();
    END IF;

    RETURN NEW;
END
$$;

DROP TRIGGER IF EXISTS trg_guard_package_price_version_update
    ON billing.package_price_versions;
CREATE TRIGGER trg_guard_package_price_version_update
BEFORE UPDATE OR DELETE ON billing.package_price_versions
FOR EACH ROW EXECUTE FUNCTION billing.guard_package_price_version_update();

CREATE TABLE IF NOT EXISTS billing.credit_wallets (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    owner_type varchar(16) NOT NULL
        CHECK (owner_type IN ('ACCOUNT', 'CLINIC')),
    owner_id uuid NOT NULL,
    environment varchar(8) NOT NULL
        CHECK (environment IN ('LIVE', 'TEST')),
    -- Available spendable credits, not a cached total balance.
    balance integer NOT NULL DEFAULT 0 CHECK (balance >= 0),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_credit_wallet_owner_environment
        UNIQUE (owner_type, owner_id, environment),
    CONSTRAINT uq_credit_wallet_id_owner_environment
        UNIQUE (id, owner_type, owner_id, environment)
);

CREATE TABLE IF NOT EXISTS billing.orders (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    billing_owner_type varchar(16) NOT NULL
        CHECK (billing_owner_type IN ('ACCOUNT', 'CLINIC')),
    billing_owner_id uuid NOT NULL,
    package_id integer NOT NULL,
    package_price_version_id uuid NOT NULL,
    package_name_snapshot varchar(100) NOT NULL,
    price_amount_snapshot numeric(12, 2) NOT NULL
        CHECK (price_amount_snapshot >= 0),
    currency_code_snapshot varchar(3) NOT NULL
        CHECK (currency_code_snapshot ~ '^[A-Z]{3}$'),
    credits_snapshot integer NOT NULL CHECK (credits_snapshot > 0),
    environment varchar(8) NOT NULL
        CHECK (environment IN ('LIVE', 'TEST')),
    status varchar(16) NOT NULL DEFAULT 'PENDING'
        CHECK (status IN ('PENDING', 'COMPLETED', 'CANCELLED')),
    idempotency_key uuid NOT NULL,
    request_fingerprint varchar(128) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_order_package_price_version
        FOREIGN KEY (package_price_version_id, package_id)
        REFERENCES billing.package_price_versions (id, package_id),
    CONSTRAINT uq_billing_order_idempotency
        UNIQUE (tenant_id, idempotency_key)
);

-- A transaction order must snapshot the exact ACTIVE version and values.
CREATE OR REPLACE FUNCTION billing.validate_order_price_snapshot()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    price_version billing.package_price_versions%ROWTYPE;
BEGIN
    SELECT * INTO price_version
    FROM billing.package_price_versions
    WHERE id = NEW.package_price_version_id
      AND package_id = NEW.package_id;

    IF NOT FOUND OR price_version.status <> 'ACTIVE' THEN
        RAISE EXCEPTION 'Orders require an ACTIVE package price version'
            USING ERRCODE = '23514';
    END IF;

    IF NEW.package_name_snapshot IS DISTINCT FROM price_version.display_name
       OR NEW.price_amount_snapshot IS DISTINCT FROM price_version.price_amount
       OR NEW.currency_code_snapshot IS DISTINCT FROM price_version.currency_code
       OR NEW.credits_snapshot IS DISTINCT FROM price_version.credits THEN
        RAISE EXCEPTION 'Order snapshot does not match its price version'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END
$$;

DROP TRIGGER IF EXISTS trg_validate_order_price_snapshot ON billing.orders;
CREATE TRIGGER trg_validate_order_price_snapshot
BEFORE INSERT ON billing.orders
FOR EACH ROW EXECUTE FUNCTION billing.validate_order_price_snapshot();

-- Order price/owner snapshots remain immutable; only lifecycle fields can change.
CREATE OR REPLACE FUNCTION billing.guard_order_snapshot_update()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.id IS DISTINCT FROM OLD.id
       OR NEW.tenant_id IS DISTINCT FROM OLD.tenant_id
       OR NEW.billing_owner_type IS DISTINCT FROM OLD.billing_owner_type
       OR NEW.billing_owner_id IS DISTINCT FROM OLD.billing_owner_id
       OR NEW.package_id IS DISTINCT FROM OLD.package_id
       OR NEW.package_price_version_id IS DISTINCT FROM OLD.package_price_version_id
       OR NEW.package_name_snapshot IS DISTINCT FROM OLD.package_name_snapshot
       OR NEW.price_amount_snapshot IS DISTINCT FROM OLD.price_amount_snapshot
       OR NEW.currency_code_snapshot IS DISTINCT FROM OLD.currency_code_snapshot
       OR NEW.credits_snapshot IS DISTINCT FROM OLD.credits_snapshot
       OR NEW.environment IS DISTINCT FROM OLD.environment
       OR NEW.idempotency_key IS DISTINCT FROM OLD.idempotency_key
       OR NEW.request_fingerprint IS DISTINCT FROM OLD.request_fingerprint
       OR NEW.created_at IS DISTINCT FROM OLD.created_at THEN
        RAISE EXCEPTION 'Order snapshots are immutable'
            USING ERRCODE = '55000';
    END IF;

    IF OLD.status IN ('COMPLETED', 'CANCELLED')
       AND NEW.status IS DISTINCT FROM OLD.status THEN
        RAISE EXCEPTION 'Terminal order state cannot be changed'
            USING ERRCODE = '55000';
    END IF;

    RETURN NEW;
END
$$;

DROP TRIGGER IF EXISTS trg_guard_order_snapshot_update ON billing.orders;
CREATE TRIGGER trg_guard_order_snapshot_update
BEFORE UPDATE ON billing.orders
FOR EACH ROW EXECUTE FUNCTION billing.guard_order_snapshot_update();

CREATE TABLE IF NOT EXISTS billing.credit_reservations (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    analysis_id uuid NOT NULL,
    generation integer NOT NULL CHECK (generation > 0),
    wallet_id uuid NOT NULL,
    actor_account_id uuid NOT NULL,
    patient_id uuid NOT NULL,
    billing_owner_type varchar(16) NOT NULL
        CHECK (billing_owner_type IN ('ACCOUNT', 'CLINIC')),
    billing_owner_id uuid NOT NULL,
    units integer NOT NULL CHECK (units = 1),
    environment varchar(8) NOT NULL
        CHECK (environment IN ('LIVE', 'TEST')),
    is_mock boolean NOT NULL DEFAULT false,
    state varchar(16) NOT NULL DEFAULT 'RESERVED'
        CHECK (state IN ('RESERVED', 'CAPTURED', 'RELEASED')),
    release_reason varchar(32)
        CHECK (release_reason IS NULL OR release_reason IN (
            'VALIDATION_ABORT', 'STORAGE_ABORT', 'ANALYSIS_ABORT',
            'FINAL_FAILURE', 'LOW_QUALITY'
        )),
    idempotency_key uuid NOT NULL,
    request_fingerprint varchar(128) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    captured_at timestamptz,
    released_at timestamptz,
    CONSTRAINT fk_reservation_wallet_owner_environment
        FOREIGN KEY (wallet_id, billing_owner_type, billing_owner_id, environment)
        REFERENCES billing.credit_wallets (id, owner_type, owner_id, environment),
    CONSTRAINT uq_credit_reservation_generation
        UNIQUE (tenant_id, analysis_id, generation),
    CONSTRAINT uq_credit_reservation_idempotency
        UNIQUE (tenant_id, idempotency_key),
    CONSTRAINT ck_mock_uses_test_wallet
        CHECK (NOT is_mock OR environment = 'TEST'),
    CONSTRAINT ck_credit_reservation_terminal_state
        CHECK (
            (state = 'RESERVED'
                AND captured_at IS NULL
                AND released_at IS NULL
                AND release_reason IS NULL)
            OR
            (state = 'CAPTURED'
                AND captured_at IS NOT NULL
                AND released_at IS NULL
                AND release_reason IS NULL)
            OR
            (state = 'RELEASED'
                AND released_at IS NOT NULL
                AND captured_at IS NULL
                AND release_reason IS NOT NULL)
        )
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_credit_reservation_one_active_per_analysis
    ON billing.credit_reservations (tenant_id, analysis_id)
    WHERE state = 'RESERVED';

CREATE INDEX IF NOT EXISTS ix_credit_reservation_reconciliation
    ON billing.credit_reservations (state, updated_at)
    WHERE state = 'RESERVED';

-- Capture/release are terminal transitions; a captured reservation can never
-- later be released, and a released reservation can never later be captured.
CREATE OR REPLACE FUNCTION billing.guard_credit_reservation_transition()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF OLD.state IN ('CAPTURED', 'RELEASED') THEN
        IF NEW.state IS DISTINCT FROM OLD.state
           OR NEW.release_reason IS DISTINCT FROM OLD.release_reason
           OR NEW.captured_at IS DISTINCT FROM OLD.captured_at
           OR NEW.released_at IS DISTINCT FROM OLD.released_at THEN
            RAISE EXCEPTION 'A terminal credit reservation cannot transition again'
                USING ERRCODE = '55000';
        END IF;
        RETURN NEW;
    END IF;

    IF OLD.state = 'RESERVED' AND NEW.state = 'CAPTURED' THEN
        NEW.captured_at := COALESCE(NEW.captured_at, now());
        NEW.released_at := NULL;
        NEW.release_reason := NULL;
    ELSIF OLD.state = 'RESERVED' AND NEW.state = 'RELEASED' THEN
        NEW.released_at := COALESCE(NEW.released_at, now());
        NEW.captured_at := NULL;
    ELSIF NEW.state = 'RESERVED' THEN
        NEW.captured_at := NULL;
        NEW.released_at := NULL;
        NEW.release_reason := NULL;
    END IF;

    RETURN NEW;
END
$$;

DROP TRIGGER IF EXISTS trg_guard_credit_reservation_transition
    ON billing.credit_reservations;
CREATE TRIGGER trg_guard_credit_reservation_transition
BEFORE UPDATE ON billing.credit_reservations
FOR EACH ROW EXECUTE FUNCTION billing.guard_credit_reservation_transition();

CREATE TABLE IF NOT EXISTS billing.credit_ledger_entries (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    wallet_id uuid NOT NULL REFERENCES billing.credit_wallets(id),
    tenant_id uuid NOT NULL,
    order_id uuid REFERENCES billing.orders(id),
    reservation_id uuid REFERENCES billing.credit_reservations(id),
    entry_type varchar(16) NOT NULL
        CHECK (entry_type IN ('CREDIT', 'RESERVE', 'CAPTURE', 'RELEASE', 'ADJUSTMENT')),
    -- Signed delta of spendable balance: CREDIT/RELEASE positive, RESERVE
    -- negative, CAPTURE zero (the credit was debited at reservation time).
    amount integer NOT NULL,
    balance_after integer NOT NULL CHECK (balance_after >= 0),
    idempotency_key uuid NOT NULL,
    request_fingerprint varchar(128) NOT NULL,
    metadata jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_credit_ledger_idempotency
        UNIQUE (tenant_id, idempotency_key),
    CONSTRAINT ck_credit_ledger_amount_by_type
        CHECK (
            (entry_type = 'CREDIT' AND amount > 0)
            OR (entry_type = 'RESERVE' AND amount < 0)
            OR (entry_type = 'CAPTURE' AND amount = 0)
            OR (entry_type = 'RELEASE' AND amount > 0)
            OR (entry_type = 'ADJUSTMENT' AND amount <> 0)
        ),
    CONSTRAINT ck_credit_ledger_reservation_required
        CHECK (entry_type NOT IN ('RESERVE', 'CAPTURE', 'RELEASE') OR reservation_id IS NOT NULL)
);

CREATE INDEX IF NOT EXISTS ix_credit_ledger_wallet_created
    ON billing.credit_ledger_entries (wallet_id, created_at DESC);

CREATE UNIQUE INDEX IF NOT EXISTS uq_credit_ledger_one_reserve_per_reservation
    ON billing.credit_ledger_entries (reservation_id)
    WHERE reservation_id IS NOT NULL AND entry_type = 'RESERVE';

-- A reservation can have one terminal ledger event: CAPTURE or RELEASE, never both.
CREATE UNIQUE INDEX IF NOT EXISTS uq_credit_ledger_one_terminal_per_reservation
    ON billing.credit_ledger_entries (reservation_id)
    WHERE reservation_id IS NOT NULL AND entry_type IN ('CAPTURE', 'RELEASE');

COMMENT ON TABLE billing.credit_ledger_entries IS
    'Append-only credit audit ledger; runtime may SELECT/INSERT but not UPDATE/DELETE.';
COMMENT ON COLUMN billing.credit_wallets.balance IS
    'Available integer credits; reserve debits, release refunds, capture does not change available balance.';
COMMENT ON COLUMN billing.credit_ledger_entries.amount IS
    'Signed delta of available credits: CREDIT/RELEASE positive, RESERVE negative, CAPTURE zero.';

-- Initial table grants. The final role migration reapplies and restricts these.
GRANT SELECT, INSERT ON billing.package_price_versions TO aura_billing_app;
GRANT UPDATE (status, currency_code, effective_to)
    ON billing.package_price_versions TO aura_billing_app;
GRANT SELECT, INSERT ON billing.credit_wallets TO aura_billing_app;
GRANT UPDATE (balance, updated_at) ON billing.credit_wallets TO aura_billing_app;
GRANT SELECT, INSERT ON billing.orders TO aura_billing_app;
GRANT UPDATE (status, updated_at) ON billing.orders TO aura_billing_app;
GRANT SELECT, INSERT ON billing.credit_ledger_entries TO aura_billing_app;
GRANT SELECT, INSERT ON billing.credit_reservations TO aura_billing_app;
GRANT UPDATE (state, release_reason, updated_at, captured_at, released_at)
    ON billing.credit_reservations TO aura_billing_app;

COMMIT;
