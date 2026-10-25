-- Durable upload intents, separate from accepted analysis_requests. No cross-service FK.
-- Apply with migration credentials after 001_analysis_core.sql, then final ownership/grants.
BEGIN;
CREATE TABLE IF NOT EXISTS analysis.upload_recovery_operations (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    requester_id uuid NOT NULL,
    idempotency_key uuid NOT NULL,
    fingerprint varchar(64) NOT NULL CHECK (fingerprint ~ '^[0-9a-f]{64}$'),
    fingerprint_version varchar(32) NOT NULL CHECK (fingerprint_version = '1'),
    patient_id uuid NOT NULL,
    owner_id uuid NOT NULL,
    billing_owner varchar(80) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (tenant_id, requester_id, idempotency_key),
    CHECK (id <> '00000000-0000-0000-0000-000000000000'::uuid
       AND tenant_id <> '00000000-0000-0000-0000-000000000000'::uuid
       AND requester_id <> '00000000-0000-0000-0000-000000000000'::uuid
       AND idempotency_key <> '00000000-0000-0000-0000-000000000000'::uuid
       AND patient_id <> '00000000-0000-0000-0000-000000000000'::uuid
       AND owner_id <> '00000000-0000-0000-0000-000000000000'::uuid),
    CHECK (billing_owner ~ '^(ACCOUNT|CLINIC):[0-9a-fA-F-]{36}$')
);
CREATE TABLE IF NOT EXISTS analysis.upload_recovery_attempts (
    id uuid PRIMARY KEY CHECK (id <> '00000000-0000-0000-0000-000000000000'::uuid),
    operation_id uuid NOT NULL REFERENCES analysis.upload_recovery_operations(id),
    attempt_no integer NOT NULL CHECK (attempt_no > 0),
    revision bigint NOT NULL DEFAULT 1 CHECK (revision > 0),
    phase varchar(16) NOT NULL CHECK (phase IN ('PENDING','ABORTING','ABORTED','ACCEPTED')),
    container varchar(63) NOT NULL CHECK (container = 'aura-originals'),
    plan jsonb NOT NULL CHECK (jsonb_typeof(plan) = 'object' AND octet_length(plan::text) <= 1048576),
    effects jsonb NOT NULL CHECK (jsonb_typeof(effects) = 'array' AND jsonb_array_length(effects) BETWEEN 1 AND 100
        AND octet_length(effects::text) <= 131072),
    abort_reason varchar(32) NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (operation_id, attempt_no),
    CHECK ((phase IN ('ABORTING','ABORTED')) = (abort_reason IS NOT NULL)),
    CHECK (abort_reason IS NULL OR abort_reason IN ('ANALYSIS_ABORT','STORAGE_ABORT','VALIDATION_ABORT'))
);
-- One unfinished/accepted attempt; ABORTED history is retained, never deleted by the runtime.
CREATE UNIQUE INDEX IF NOT EXISTS uq_upload_recovery_active
    ON analysis.upload_recovery_attempts(operation_id) WHERE phase <> 'ABORTED';
CREATE INDEX IF NOT EXISTS ix_upload_recovery_due
    ON analysis.upload_recovery_attempts(updated_at,id) WHERE phase IN ('PENDING','ABORTING');
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname='aura_analysis_app') THEN
        GRANT USAGE ON SCHEMA analysis TO aura_analysis_app;
        GRANT SELECT, INSERT ON analysis.upload_recovery_operations TO aura_analysis_app;
        GRANT SELECT, INSERT ON analysis.upload_recovery_attempts TO aura_analysis_app;
        REVOKE UPDATE ON analysis.upload_recovery_attempts FROM aura_analysis_app;
        GRANT UPDATE (revision,phase,effects,abort_reason,updated_at)
            ON analysis.upload_recovery_attempts TO aura_analysis_app;
        REVOKE UPDATE, DELETE ON analysis.upload_recovery_operations FROM aura_analysis_app;
        REVOKE DELETE ON analysis.upload_recovery_attempts FROM aura_analysis_app;
    END IF;
END $$;
COMMIT;
