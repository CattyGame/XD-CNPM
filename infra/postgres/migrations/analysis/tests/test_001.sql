-- SCRUM-302: catalog/permission assertions for analysis schema.
-- Run only against an isolated test database after applying 001_analysis_core.sql.
-- Usage: psql -X -v ON_ERROR_STOP=1 "$AURA_TEST_DATABASE_URL" -f test_001.sql
-- A failed assertion raises an exception and psql exits non-zero.
\set ON_ERROR_STOP on
\pset pager off

BEGIN;
SET LOCAL statement_timeout = '15s';

DO $assertions$
DECLARE
    missing text[];
    runtime_role constant text := 'aura_analysis_app';
    required_constraints text[] := ARRAY[
        'uq_analysis_requests_idempotency', 'ck_analysis_requests_fingerprint',
        'fk_images_request', 'fk_images_existing', 'ck_images_modality',
        'ck_images_duplicate_ref', 'uq_jobs_message_id', 'uq_jobs_image_attempt',
        'fk_jobs_image', 'uq_results_image_version', 'uq_results_job',
        'fk_results_image', 'fk_results_job', 'fk_outbox_request', 'fk_outbox_image',
        'fk_annotation_result', 'uq_reviews_image_version', 'fk_reviews_result',
        'ck_reviews_version', 'ck_reviews_correction', 'fk_audit_request', 'fk_audit_image'
    ];
    required_tables text[] := ARRAY[
        'analysis_requests', 'images', 'jobs', 'analysis_results',
        'outbox_events', 'annotation_refs', 'doctor_reviews', 'audit_logs'
    ];
    t text;
BEGIN
    IF current_setting('server_version_num')::integer < 170000 THEN
        RAISE EXCEPTION 'FAIL: PostgreSQL 17+ required; found %', current_setting('server_version');
    END IF;

    SELECT array_agg(x) INTO missing
    FROM unnest(required_tables) AS x
    WHERE to_regclass(format('analysis.%I', x)) IS NULL;
    IF missing IS NOT NULL THEN
        RAISE EXCEPTION 'FAIL: missing analysis tables: %', array_to_string(missing, ', ');
    END IF;

    SELECT array_agg(x) INTO missing
    FROM unnest(required_constraints) AS x
    WHERE NOT EXISTS (
        SELECT 1 FROM pg_constraint c
        JOIN pg_namespace n ON n.oid = c.connamespace
        WHERE n.nspname = 'analysis' AND c.conname = x
    );
    IF missing IS NOT NULL THEN
        RAISE EXCEPTION 'FAIL: missing required constraints: %', array_to_string(missing, ', ');
    END IF;

    -- Behavioral assertions: these writes live inside this script's transaction and are rolled back.
    -- Fixture IDs are fixed so the run is repeatable on an isolated database.
    INSERT INTO analysis.analysis_requests
        (id, tenant_id, owner_id, requester_id, patient_id, idempotency_key,
         request_fingerprint, request_schema_version, accepted_response, correlation_id)
    VALUES
        ('a1000000-0000-4000-8000-000000000001', '11111111-1111-1111-1111-111111111111',
         '33333333-3333-3333-3333-333333333333', '33333333-3333-3333-3333-333333333333',
         '44444444-4444-4444-4444-444444444444', 'b1000000-0000-4000-8000-000000000001',
         repeat('a', 64), '0.6', '{}'::jsonb, 'sql-test-1');

    BEGIN
        INSERT INTO analysis.analysis_requests
            (id, tenant_id, owner_id, requester_id, patient_id, idempotency_key,
             request_fingerprint, request_schema_version, accepted_response, correlation_id)
        VALUES
            ('a1000000-0000-4000-8000-000000000002', '11111111-1111-1111-1111-111111111111',
             '33333333-3333-3333-3333-333333333333', '33333333-3333-3333-3333-333333333333',
             '44444444-4444-4444-4444-444444444444', 'b1000000-0000-4000-8000-000000000001',
             repeat('a', 64), '0.6', '{}'::jsonb, 'sql-test-2');
        RAISE EXCEPTION 'FAIL: duplicate idempotency scope was accepted';
    EXCEPTION WHEN unique_violation THEN
        GET STACKED DIAGNOSTICS t = CONSTRAINT_NAME;
        IF t <> 'uq_analysis_requests_idempotency' THEN
            RAISE EXCEPTION 'FAIL: duplicate idempotency rejected by %, expected uq_analysis_requests_idempotency', t;
        END IF;
    END;

    -- Same key under a different tenant must be independent.
    INSERT INTO analysis.analysis_requests
        (id, tenant_id, owner_id, requester_id, patient_id, idempotency_key,
         request_fingerprint, request_schema_version, accepted_response, correlation_id)
    VALUES
        ('a1000000-0000-4000-8000-000000000003', '22222222-2222-2222-2222-222222222222',
         '33333333-3333-3333-3333-333333333333', '33333333-3333-3333-3333-333333333333',
         '44444444-4444-4444-4444-444444444444', 'b1000000-0000-4000-8000-000000000001',
         repeat('a', 64), '0.6', '{}'::jsonb, 'sql-test-3');

    BEGIN
        INSERT INTO analysis.analysis_requests
            (id, tenant_id, owner_id, requester_id, patient_id, idempotency_key,
             request_fingerprint, request_schema_version, accepted_response, correlation_id)
        VALUES
            ('a1000000-0000-4000-8000-000000000004', '11111111-1111-1111-1111-111111111111',
             '33333333-3333-3333-3333-333333333333', '33333333-3333-3333-3333-333333333333',
             '44444444-4444-4444-4444-444444444444', 'b1000000-0000-4000-8000-000000000004',
             'not-a-sha256', '0.6', '{}'::jsonb, 'sql-test-4');
        RAISE EXCEPTION 'FAIL: invalid fingerprint was accepted';
    EXCEPTION WHEN check_violation THEN
        GET STACKED DIAGNOSTICS t = CONSTRAINT_NAME;
        IF t <> 'ck_analysis_requests_fingerprint' THEN
            RAISE EXCEPTION 'FAIL: invalid fingerprint rejected by %, expected ck_analysis_requests_fingerprint', t;
        END IF;
    END;

    BEGIN
        INSERT INTO analysis.images
            (id, tenant_id, analysis_request_id, file_id, modality, eye_side, object_key,
             mime_type, size_bytes, width, height, sha256, processing_status)
        VALUES
            ('a2000000-0000-4000-8000-000000000001', '11111111-1111-1111-1111-111111111111',
             'a1000000-0000-4000-8000-000000000001', 'a3000000-0000-4000-8000-000000000001',
             'OCT', 'left', 'sql-test/image-1', 'image/png', 100, 10, 10, repeat('b', 64), 'QUEUED');
        RAISE EXCEPTION 'FAIL: unsupported modality was accepted';
    EXCEPTION WHEN check_violation THEN
        GET STACKED DIAGNOSTICS t = CONSTRAINT_NAME;
        IF t <> 'ck_images_modality' THEN
            RAISE EXCEPTION 'FAIL: modality rejected by %, expected ck_images_modality', t;
        END IF;
    END;

    BEGIN
        INSERT INTO analysis.images
            (id, tenant_id, analysis_request_id, file_id, modality, eye_side, object_key,
             mime_type, size_bytes, width, height, sha256, processing_status)
        VALUES
            ('a2000000-0000-4000-8000-000000000002', '22222222-2222-2222-2222-222222222222',
             'a1000000-0000-4000-8000-000000000001', 'a3000000-0000-4000-8000-000000000002',
             'FUNDUS', 'left', 'sql-test/image-2', 'image/png', 100, 10, 10, repeat('b', 64), 'QUEUED');
        RAISE EXCEPTION 'FAIL: cross-tenant request reference was accepted';
    EXCEPTION WHEN foreign_key_violation THEN
        GET STACKED DIAGNOSTICS t = CONSTRAINT_NAME;
        IF t <> 'fk_images_request' THEN
            RAISE EXCEPTION 'FAIL: cross-tenant reference rejected by %, expected fk_images_request', t;
        END IF;
    END;

    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname = 'analysis' AND indexname = 'ix_outbox_unpublished'
          AND indexdef ILIKE '%WHERE (published_at IS NULL)%'
    ) THEN
        RAISE EXCEPTION 'FAIL: ix_outbox_unpublished must be a partial index for unpublished events';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = runtime_role) THEN
        RAISE EXCEPTION 'FAIL: runtime role % is missing; provision it before testing privileges', runtime_role;
    END IF;
    IF NOT has_schema_privilege(runtime_role, 'analysis', 'USAGE') THEN
        RAISE EXCEPTION 'FAIL: % lacks USAGE on schema analysis', runtime_role;
    END IF;
    IF has_schema_privilege(runtime_role, 'analysis', 'CREATE') THEN
        RAISE EXCEPTION 'FAIL: % must not have CREATE on schema analysis', runtime_role;
    END IF;
    IF has_database_privilege(runtime_role, current_database(), 'CREATE') THEN
        RAISE EXCEPTION 'FAIL: % must not have CREATE on database %', runtime_role, current_database();
    END IF;

    FOREACH t IN ARRAY required_tables LOOP
        IF NOT has_table_privilege(runtime_role, format('analysis.%I', t), 'SELECT') THEN
            RAISE EXCEPTION 'FAIL: % lacks SELECT on analysis.%', runtime_role, t;
        END IF;
        IF NOT has_table_privilege(runtime_role, format('analysis.%I', t), 'INSERT') THEN
            RAISE EXCEPTION 'FAIL: % lacks INSERT on analysis.%', runtime_role, t;
        END IF;
        IF has_table_privilege(runtime_role, format('analysis.%I', t), 'DELETE') THEN
            RAISE EXCEPTION 'FAIL: % unexpectedly has DELETE on analysis.%', runtime_role, t;
        END IF;
    END LOOP;

    FOREACH t IN ARRAY ARRAY['analysis_requests','images','jobs','outbox_events','annotation_refs'] LOOP
        IF NOT has_table_privilege(runtime_role, format('analysis.%I', t), 'UPDATE') THEN
            RAISE EXCEPTION 'FAIL: % lacks UPDATE on mutable table analysis.%', runtime_role, t;
        END IF;
    END LOOP;

    FOREACH t IN ARRAY ARRAY['analysis_results','doctor_reviews','audit_logs'] LOOP
        IF has_table_privilege(runtime_role, format('analysis.%I', t), 'UPDATE') THEN
            RAISE EXCEPTION 'FAIL: % must not UPDATE append-only table analysis.%', runtime_role, t;
        END IF;
    END LOOP;

    -- No foreign keys from Analysis into other service schemas.
    IF EXISTS (
        SELECT 1
        FROM pg_constraint c
        JOIN pg_class source_table ON source_table.oid = c.conrelid
        JOIN pg_namespace source_schema ON source_schema.oid = source_table.relnamespace
        JOIN pg_class referenced_table ON referenced_table.oid = c.confrelid
        JOIN pg_namespace referenced_schema ON referenced_schema.oid = referenced_table.relnamespace
        WHERE c.contype = 'f'
          AND source_schema.nspname = 'analysis'
          AND referenced_schema.nspname <> 'analysis'
    ) THEN
        RAISE EXCEPTION 'FAIL: cross-schema foreign key found from Analysis';
    END IF;

    RAISE NOTICE 'PASS: PostgreSQL %, 8 tables, % required constraints, partial outbox index and runtime grants',
        current_setting('server_version'), cardinality(required_constraints);
END
$assertions$;

ROLLBACK;
