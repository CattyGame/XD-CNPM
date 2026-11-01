-- SCRUM-326: durable Billing settlement inbox.
-- Event payloads are not stored; only validated routing metadata and SHA-256 fingerprints are retained.
BEGIN;

DO $$
BEGIN
    IF current_database() <> 'aura_dev' THEN
        RAISE EXCEPTION 'Run this migration only on aura_dev';
    END IF;
END
$$;

SET LOCAL ROLE aura_billing_migrator;

CREATE TABLE IF NOT EXISTS billing.settlement_inbox (
    consumer_name              varchar(64)  NOT NULL,
    event_id                   uuid         NOT NULL,
    event_type                 varchar(32)  NOT NULL,
    schema_version             varchar(16)  NOT NULL,
    occurred_at                timestamptz  NOT NULL,
    tenant_id                  uuid         NOT NULL,
    analysis_id                uuid         NOT NULL,
    correlation_id             varchar(64)  NOT NULL,
    payload_sha256              varchar(64)  NOT NULL,
    conflict_payload_sha256     varchar(64),
    conflict_prior_status         varchar(24),
    conflict_prior_settlement_action varchar(16),
    conflict_prior_settlement_reason varchar(32),
    conflict_prior_completed_at   timestamptz,
    status                     varchar(24)  NOT NULL DEFAULT 'PENDING',
    resolver_attempts           smallint     NOT NULL DEFAULT 0,
    next_attempt_at             timestamptz  DEFAULT now(),
    lease_token                uuid,
    lease_until                timestamptz,
    last_error_code             varchar(64),
    resolved_reservation_id     uuid,
    resolved_generation         integer,
    result_version              integer,
    is_mock                     boolean,
    processing_status           varchar(32),
    settlement_action           varchar(16),
    settlement_reason           varchar(32),
    created_at                  timestamptz  NOT NULL DEFAULT now(),
    updated_at                  timestamptz  NOT NULL DEFAULT now(),
    completed_at                timestamptz,
    CONSTRAINT pk_billing_settlement_inbox PRIMARY KEY (consumer_name, event_id),
    CONSTRAINT ck_settlement_inbox_consumer CHECK (length(btrim(consumer_name)) BETWEEN 1 AND 64),
    CONSTRAINT ck_settlement_inbox_event_type CHECK (event_type IN ('analysis.completed', 'analysis.failed')),
    CONSTRAINT ck_settlement_inbox_schema CHECK (schema_version = '0.7'),
    CONSTRAINT ck_settlement_inbox_correlation CHECK (correlation_id ~ '^[A-Za-z0-9._-]{1,64}$'),
    CONSTRAINT ck_settlement_inbox_payload_hash CHECK (payload_sha256 ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_settlement_inbox_conflict_hash CHECK (
        conflict_payload_sha256 IS NULL OR conflict_payload_sha256 ~ '^[0-9a-f]{64}$'
    ),
    CONSTRAINT ck_settlement_inbox_status CHECK (
        status IN ('PENDING', 'COMPLETED', 'IGNORED', 'HOLD', 'RECONCILE_REQUIRED')
    ),
    CONSTRAINT ck_settlement_inbox_attempts CHECK (resolver_attempts BETWEEN 0 AND 3),
    CONSTRAINT ck_settlement_inbox_error_code CHECK (
        last_error_code IS NULL OR last_error_code ~ '^[A-Z0-9_]{1,64}$'
    ),
    CONSTRAINT ck_settlement_inbox_generation CHECK (
        resolved_generation IS NULL OR resolved_generation > 0
    ),
    CONSTRAINT ck_settlement_inbox_result_version CHECK (
        result_version IS NULL OR result_version > 0
    ),
    CONSTRAINT ck_settlement_inbox_schedule CHECK (
        (status = 'PENDING' AND next_attempt_at IS NOT NULL)
        OR (status <> 'PENDING' AND next_attempt_at IS NULL)
    ),
    CONSTRAINT ck_settlement_inbox_lease_pair CHECK (
        (lease_token IS NULL AND lease_until IS NULL)
        OR (lease_token IS NOT NULL AND lease_until IS NOT NULL AND status = 'PENDING')
    ),
    CONSTRAINT ck_settlement_inbox_terminal_time CHECK (
        (status IN ('COMPLETED', 'IGNORED') AND completed_at IS NOT NULL)
        OR (status NOT IN ('COMPLETED', 'IGNORED') AND completed_at IS NULL)
    ),
    CONSTRAINT ck_settlement_inbox_action CHECK (
        (
            status = 'COMPLETED'
            AND resolved_reservation_id IS NOT NULL
            AND resolved_generation IS NOT NULL
            AND (
                (
                    settlement_action = 'CAPTURE'
                    AND event_type = 'analysis.completed'
                    AND settlement_reason = 'VALID_RESULT'
                    AND processing_status = 'COMPLETED'
                    AND result_version IS NOT NULL
                    AND is_mock IS FALSE
                )
                OR
                (
                    settlement_action = 'RELEASE'
                    AND event_type = 'analysis.failed'
                    AND settlement_reason IN (
                        'VALIDATION_ABORT', 'STORAGE_ABORT', 'ANALYSIS_ABORT',
                        'FINAL_FAILURE', 'LOW_QUALITY'
                    )
                    AND processing_status IN ('FAILED', 'LOW_QUALITY', 'REJECTED')
                    AND result_version IS NULL
                    AND is_mock IS FALSE
                )
            )
        )
        OR
        (
            status = 'IGNORED'
            AND settlement_action = 'NONE'
            AND settlement_reason IN ('NO_RESERVATION', 'NOT_APPLICABLE')
        )
        OR
        (
            status IN ('PENDING', 'HOLD', 'RECONCILE_REQUIRED')
            AND settlement_action IS NULL
            AND settlement_reason IS NULL
        )
    )
);

-- Upgrade an existing settlement inbox created before conflict audit fields were added.
ALTER TABLE billing.settlement_inbox
    ADD COLUMN IF NOT EXISTS conflict_prior_status varchar(24),
    ADD COLUMN IF NOT EXISTS conflict_prior_settlement_action varchar(16),
    ADD COLUMN IF NOT EXISTS conflict_prior_settlement_reason varchar(32),
    ADD COLUMN IF NOT EXISTS conflict_prior_completed_at timestamptz;

CREATE UNIQUE INDEX IF NOT EXISTS uq_billing_settlement_inbox_lease_token
    ON billing.settlement_inbox (lease_token)
    WHERE lease_token IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_billing_settlement_inbox_due
    ON billing.settlement_inbox (next_attempt_at, created_at)
    WHERE status = 'PENDING';

CREATE INDEX IF NOT EXISTS ix_billing_settlement_inbox_stale
    ON billing.settlement_inbox (created_at)
    WHERE status IN ('PENDING', 'HOLD', 'RECONCILE_REQUIRED');

REVOKE ALL ON billing.settlement_inbox FROM PUBLIC;
REVOKE INSERT, UPDATE, DELETE ON billing.settlement_inbox FROM aura_billing_app;
GRANT SELECT ON billing.settlement_inbox TO aura_billing_app;
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

COMMIT;