-- Transactional tests for Billing price-version currency rules
-- Run after migration 004 on aura_dev with psql -v ON_ERROR_STOP=1.
-- All test data is rolled back.

BEGIN;
SET LOCAL ROLE aura_billing_migrator;

DO $test$
DECLARE
    test_package_id integer :=
        1900000000 + floor(random() * 1000000)::integer;
    test_price_version_id uuid := gen_random_uuid();
    test_demo_version_id uuid := gen_random_uuid();
    test_tenant_id uuid := gen_random_uuid();
    test_owner_id uuid := gen_random_uuid();
    actual_currency varchar(3);
    actual_status varchar(16);
BEGIN
    -- Create a DRAFT whose currency has not yet been decided.
    INSERT INTO billing.package_price_versions
        (id, package_id, version_number, display_name, price_amount,
         currency_code, credits, status, effective_from)
    VALUES
        (test_price_version_id, test_package_id, 91001,
         'SCRUM-306 price-version test', 12.50,
         NULL, 5, 'DRAFT', now() - interval '1 day');

    -- Currency may be assigned while the price version is DRAFT.
    UPDATE billing.package_price_versions
       SET currency_code = 'USD'
     WHERE id = test_price_version_id;

    SELECT currency_code
      INTO actual_currency
      FROM billing.package_price_versions
     WHERE id = test_price_version_id;

    IF actual_currency IS DISTINCT FROM 'USD' THEN
        RAISE EXCEPTION 'DRAFT currency update was not persisted';
    END IF;

    -- Invalid lowercase currency must be rejected by the CHECK constraint.
    BEGIN
        UPDATE billing.package_price_versions
           SET currency_code = 'usd'
         WHERE id = test_price_version_id;

        RAISE EXCEPTION USING
            ERRCODE = 'P0001',
            MESSAGE = 'Expected invalid currency code to be rejected';
    EXCEPTION WHEN SQLSTATE '23514' THEN
        NULL;
    END;

    -- Publish the decided currency as an ACTIVE price version.
    UPDATE billing.package_price_versions
       SET status = 'ACTIVE'
     WHERE id = test_price_version_id;

    SELECT status, currency_code
      INTO actual_status, actual_currency
      FROM billing.package_price_versions
     WHERE id = test_price_version_id;

    IF actual_status IS DISTINCT FROM 'ACTIVE'
       OR actual_currency IS DISTINCT FROM 'USD' THEN
        RAISE EXCEPTION 'Price version activation invariant failed';
    END IF;

    -- An ACTIVE price version cannot change currency.
    BEGIN
        UPDATE billing.package_price_versions
           SET currency_code = 'EUR'
         WHERE id = test_price_version_id;

        RAISE EXCEPTION USING
            ERRCODE = 'P0001',
            MESSAGE = 'Expected ACTIVE currency update to be rejected';
    EXCEPTION WHEN SQLSTATE '55000' THEN
        NULL;
    END;

    -- The matching order snapshot must be accepted.
    INSERT INTO billing.orders
        (tenant_id, billing_owner_type, billing_owner_id,
         package_id, package_price_version_id,
         package_name_snapshot, price_amount_snapshot,
         currency_code_snapshot, credits_snapshot, environment,
         idempotency_key, request_fingerprint)
    VALUES
        (test_tenant_id, 'ACCOUNT', test_owner_id,
         test_package_id, test_price_version_id,
         'SCRUM-306 price-version test', 12.50,
         'USD', 5, 'TEST', gen_random_uuid(), repeat('a', 64));

    -- A mismatching currency snapshot must be rejected.
    BEGIN
        INSERT INTO billing.orders
            (tenant_id, billing_owner_type, billing_owner_id,
             package_id, package_price_version_id,
             package_name_snapshot, price_amount_snapshot,
             currency_code_snapshot, credits_snapshot, environment,
             idempotency_key, request_fingerprint)
        VALUES
            (test_tenant_id, 'ACCOUNT', test_owner_id,
             test_package_id, test_price_version_id,
             'SCRUM-306 price-version test', 12.50,
             'EUR', 5, 'TEST', gen_random_uuid(), repeat('b', 64));

        RAISE EXCEPTION USING
            ERRCODE = 'P0001',
            MESSAGE = 'Expected mismatching order currency snapshot to fail';
    EXCEPTION WHEN SQLSTATE '23514' THEN
        NULL;
    END;

    -- Retiring an ACTIVE version is allowed; changing currency afterward is not.
    UPDATE billing.package_price_versions
       SET status = 'RETIRED'
     WHERE id = test_price_version_id;

    BEGIN
        UPDATE billing.package_price_versions
           SET currency_code = 'EUR'
         WHERE id = test_price_version_id;

        RAISE EXCEPTION USING
            ERRCODE = 'P0001',
            MESSAGE = 'Expected RETIRED currency update to be rejected';
    EXCEPTION WHEN SQLSTATE '55000' THEN
        NULL;
    END;

    -- DEMO records must remain immutable and have no inferred currency.
    INSERT INTO billing.package_price_versions
        (id, package_id, version_number, display_name, price_amount,
         currency_code, credits, status)
    VALUES
        (test_demo_version_id, test_package_id, 91002,
         'SCRUM-306 demo test', 20.00, NULL, 10, 'DEMO');

    BEGIN
        UPDATE billing.package_price_versions
           SET currency_code = 'USD'
         WHERE id = test_demo_version_id;

        RAISE EXCEPTION USING
            ERRCODE = 'P0001',
            MESSAGE = 'Expected DEMO currency update to be rejected';
    EXCEPTION WHEN SQLSTATE '55000' THEN
        NULL;
    END;

    BEGIN
        DELETE FROM billing.package_price_versions
         WHERE id = test_demo_version_id;

        RAISE EXCEPTION USING
            ERRCODE = 'P0001',
            MESSAGE = 'Expected price-version delete to be rejected';
    EXCEPTION WHEN SQLSTATE '55000' THEN
        NULL;
    END;

    RAISE NOTICE
        'PASS: DRAFT currency, invalid currency, ACTIVE/RETIRED/DEMO guards and order currency snapshot.';
END
$test$;

ROLLBACK;