BEGIN;

DO $$
BEGIN
    IF current_database() <> 'aura_dev' THEN
        RAISE EXCEPTION 'Run this migration only on aura_dev';
    END IF;
END
$$;

-- Runtime accounts must not create objects in the shared public schema.
REVOKE CREATE ON SCHEMA public FROM PUBLIC;

DO $$
DECLARE
    service_name text;
    schema_name text;
    app_role text;
    migration_role text;
    creator_role text;
    schema_names text[];
    obj record;
    object_kind text;
BEGIN
    FOREACH service_name IN ARRAY ARRAY[
        'identity', 'profile', 'analysis', 'billing', 'notification'
    ]
    LOOP
        app_role := 'aura_' || service_name || '_app';
        migration_role := 'aura_' || service_name || '_migrator';

        IF NOT EXISTS (
            SELECT 1 FROM pg_roles WHERE rolname = app_role
        ) THEN
            EXECUTE format(
                'CREATE ROLE %I NOLOGIN NOSUPERUSER NOCREATEDB '
                'NOCREATEROLE NOREPLICATION NOBYPASSRLS',
                app_role
            );
        END IF;

        IF NOT EXISTS (
            SELECT 1 FROM pg_roles WHERE rolname = migration_role
        ) THEN
            EXECUTE format(
                'CREATE ROLE %I NOLOGIN NOSUPERUSER NOCREATEDB '
                'NOCREATEROLE NOREPLICATION NOBYPASSRLS',
                migration_role
            );
        END IF;

        -- Do not silently reuse privileged or inherited roles.
        IF EXISTS (
            SELECT 1
            FROM pg_roles
            WHERE rolname IN (app_role, migration_role)
              AND (
                  rolsuper OR rolcreatedb OR rolcreaterole
                  OR rolreplication OR rolbypassrls
              )
        ) THEN
            RAISE EXCEPTION 'Unexpected privileged role for %', service_name;
        END IF;

        IF EXISTS (
            SELECT 1
            FROM pg_auth_members m
            JOIN pg_roles r ON r.oid = m.member
            WHERE r.rolname IN (app_role, migration_role)
        ) THEN
            RAISE EXCEPTION 'Unexpected role membership for %', service_name;
        END IF;

        EXECUTE format(
            'GRANT CONNECT ON DATABASE aura_dev TO %I, %I',
            app_role, migration_role
        );

        schema_names := ARRAY[service_name];

        IF service_name = 'billing' THEN
            schema_names := array_append(schema_names, 'billing_poc');
        END IF;

        -- New migration functions are not executable by everyone.
        EXECUTE format(
            'ALTER DEFAULT PRIVILEGES FOR ROLE %I '
            'REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC',
            migration_role
        );

        FOREACH schema_name IN ARRAY schema_names
        LOOP
            EXECUTE format(
                'CREATE SCHEMA IF NOT EXISTS %I AUTHORIZATION %I',
                schema_name, migration_role
            );

            -- Existing schemas must belong to the known administrator
            -- or to the expected migration role.
            IF EXISTS (
                SELECT 1
                FROM pg_namespace n
                JOIN pg_roles r ON r.oid = n.nspowner
                WHERE n.nspname = schema_name
                  AND r.rolname NOT IN ('aura_dev', migration_role)
            ) THEN
                RAISE EXCEPTION 'Unexpected owner of schema %', schema_name;
            END IF;

            -- Transfer tables before sequences, including serial ownership.
            FOR obj IN
                SELECT c.relname, c.relkind,
                       pg_get_userbyid(c.relowner) AS owner_name
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = schema_name
                  AND c.relkind IN ('r', 'p', 'v', 'm', 'S', 'f')
                ORDER BY CASE WHEN c.relkind = 'S' THEN 1 ELSE 0 END,
                         c.relname
            LOOP
                IF obj.owner_name NOT IN ('aura_dev', migration_role) THEN
                    RAISE EXCEPTION 'Unexpected owner of %.%',
                        schema_name, obj.relname;
                END IF;

                object_kind := CASE obj.relkind
                    WHEN 'S' THEN 'SEQUENCE'
                    WHEN 'v' THEN 'VIEW'
                    WHEN 'm' THEN 'MATERIALIZED VIEW'
                    WHEN 'f' THEN 'FOREIGN TABLE'
                    ELSE 'TABLE'
                END;

                EXECUTE format(
                    'ALTER %s %I.%I OWNER TO %I',
                    object_kind, schema_name, obj.relname, migration_role
                );
            END LOOP;

            EXECUTE format(
                'ALTER SCHEMA %I OWNER TO %I',
                schema_name, migration_role
            );

            EXECUTE format(
                'REVOKE ALL ON SCHEMA %I FROM PUBLIC', schema_name
            );
            EXECUTE format(
                'REVOKE ALL ON ALL TABLES IN SCHEMA %I FROM PUBLIC',
                schema_name
            );
            EXECUTE format(
                'REVOKE ALL ON ALL SEQUENCES IN SCHEMA %I FROM PUBLIC',
                schema_name
            );
            EXECUTE format(
                'REVOKE EXECUTE ON ALL ROUTINES IN SCHEMA %I FROM PUBLIC',
                schema_name
            );

            EXECUTE format(
                'GRANT USAGE ON SCHEMA %I TO %I',
                schema_name, app_role
            );
            EXECUTE format(
                'GRANT SELECT, INSERT, UPDATE, DELETE '
                'ON ALL TABLES IN SCHEMA %I TO %I',
                schema_name, app_role
            );
            EXECUTE format(
                'GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA %I TO %I',
                schema_name, app_role
            );

            -- Cover migrations run as the new owner and the legacy admin.
            FOREACH creator_role IN ARRAY ARRAY[migration_role, 'aura_dev']
            LOOP
                EXECUTE format(
                    'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA %I '
                    'GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO %I',
                    creator_role, schema_name, app_role
                );

                EXECUTE format(
                    'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA %I '
                    'GRANT USAGE, SELECT ON SEQUENCES TO %I',
                    creator_role, schema_name, app_role
                );
            END LOOP;
        END LOOP;
    END LOOP;
END
$$;

-- SCRUM-300: preserve Analysis append-only permissions.
-- New Analysis tables receive SELECT/INSERT by default.
-- Service migrations explicitly grant UPDATE where required.
DO $$
DECLARE
    creator_role text;
    table_name text;
BEGIN
    FOREACH creator_role IN ARRAY ARRAY[
        'aura_analysis_migrator', 'aura_dev'
    ]
    LOOP
        EXECUTE format(
            'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA analysis '
            'REVOKE UPDATE, DELETE ON TABLES FROM aura_analysis_app',
            creator_role
        );
    END LOOP;

    FOREACH table_name IN ARRAY ARRAY[
        'analysis_results', 'doctor_reviews', 'audit_logs', 'upload_recovery_operations'
    ]
    LOOP
        IF to_regclass(format('analysis.%I', table_name)) IS NOT NULL THEN
            EXECUTE format(
                'REVOKE UPDATE, DELETE ON TABLE analysis.%I '
                'FROM aura_analysis_app',
                table_name
            );
        END IF;
    END LOOP;
END
$$;

-- Recovery plans/bindings are immutable; only lifecycle/checkpoint columns may change.
DO $$ BEGIN
    IF to_regclass('analysis.upload_recovery_attempts') IS NOT NULL THEN
        REVOKE UPDATE, DELETE ON analysis.upload_recovery_attempts FROM aura_analysis_app;
        GRANT UPDATE (revision,phase,effects,abort_reason,updated_at)
            ON analysis.upload_recovery_attempts TO aura_analysis_app;
    END IF;
END $$;

-- SCRUM-306: restrict runtime access to Billing financial records.
-- Keep the ledger append-only and limit updates to lifecycle/balance columns.
DO $$
BEGIN
    IF to_regclass('billing.package_price_versions') IS NOT NULL THEN
        EXECUTE
            'REVOKE UPDATE, DELETE ON billing.package_price_versions FROM aura_billing_app';
        EXECUTE
            'GRANT UPDATE (status, currency_code, effective_to) ON billing.package_price_versions TO aura_billing_app';
    END IF;

    IF to_regclass('billing.credit_wallets') IS NOT NULL THEN
        EXECUTE
            'REVOKE UPDATE, DELETE ON billing.credit_wallets FROM aura_billing_app';
        EXECUTE
            'GRANT UPDATE (balance, updated_at) ON billing.credit_wallets TO aura_billing_app';
    END IF;

    IF to_regclass('billing.orders') IS NOT NULL THEN
        EXECUTE
            'REVOKE UPDATE, DELETE ON billing.orders FROM aura_billing_app';
        EXECUTE
            'GRANT UPDATE (status, updated_at) ON billing.orders TO aura_billing_app';
    END IF;

    IF to_regclass('billing.credit_reservations') IS NOT NULL THEN
        EXECUTE
            'REVOKE UPDATE, DELETE ON billing.credit_reservations FROM aura_billing_app';
        EXECUTE
            'GRANT UPDATE (state, release_reason, updated_at, captured_at, released_at) ON billing.credit_reservations TO aura_billing_app';
    END IF;

    IF to_regclass('billing.credit_ledger_entries') IS NOT NULL THEN
        EXECUTE
            'REVOKE UPDATE, DELETE ON billing.credit_ledger_entries FROM aura_billing_app';
    END IF;
END
$$;

-- SCRUM-326: limit settlement inbox lifecycle updates; preserve event identity and payload fingerprint.
DO $$
BEGIN
    IF to_regclass('billing.settlement_inbox') IS NOT NULL THEN
        REVOKE INSERT, UPDATE, DELETE ON billing.settlement_inbox FROM aura_billing_app;
        GRANT INSERT (
            consumer_name,
            event_id,
            event_type,
            schema_version,
            occurred_at,
            tenant_id,
            analysis_id,
            correlation_id,
            payload_sha256
        ) ON billing.settlement_inbox TO aura_billing_app;
        GRANT UPDATE (
            conflict_payload_sha256,
            conflict_prior_status,
            conflict_prior_settlement_action,
            conflict_prior_settlement_reason,
            conflict_prior_completed_at,
            status,
            resolver_attempts,
            next_attempt_at,
            lease_token,
            lease_until,
            last_error_code,
            resolved_reservation_id,
            resolved_generation,
            result_version,
            is_mock,
            processing_status,
            settlement_action,
            settlement_reason,
            completed_at,
            updated_at
        ) ON billing.settlement_inbox TO aura_billing_app;
    END IF;
END
$$;
COMMIT;
