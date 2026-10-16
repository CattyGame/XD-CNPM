-- SCRUM-302 / W2-M2-01: Analysis database core
-- Nguon thiet ke: Database ERD Analysis v0.4 (SCRUM-53), contract Analysis REST 0.6.0, event/job schemaVersion 0.6.
-- Chay boi migration role (co DDL). Khong tao FK sang schema khac (identity/profile/billing).
-- Chay lai nhieu lan an toan (IF NOT EXISTS).

BEGIN;

CREATE SCHEMA IF NOT EXISTS analysis;

-- 1. analysis_requests (batch). id = batchId
CREATE TABLE IF NOT EXISTS analysis.analysis_requests (
    id                      uuid         NOT NULL,
    tenant_id               uuid         NOT NULL,
    owner_id                uuid         NOT NULL,
    requester_id            uuid         NOT NULL,
    patient_id              uuid         NOT NULL,
    idempotency_key         uuid         NOT NULL,
    request_fingerprint     varchar(64)  NOT NULL,
    request_schema_version  varchar(32)  NOT NULL,
    accepted_response       jsonb        NOT NULL,
    correlation_id          varchar(64)  NOT NULL,
    created_at              timestamptz  NOT NULL DEFAULT now(),
    updated_at              timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT pk_analysis_requests PRIMARY KEY (id),
    CONSTRAINT uq_analysis_requests_idempotency UNIQUE (tenant_id, requester_id, idempotency_key),
    CONSTRAINT uq_analysis_requests_tenant_id UNIQUE (tenant_id, id),
    CONSTRAINT ck_analysis_requests_fingerprint CHECK (request_fingerprint ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_analysis_requests_correlation CHECK (length(correlation_id) BETWEEN 1 AND 64)
);
CREATE INDEX IF NOT EXISTS ix_analysis_requests_patient ON analysis.analysis_requests (tenant_id, patient_id, created_at);
CREATE INDEX IF NOT EXISTS ix_analysis_requests_owner   ON analysis.analysis_requests (tenant_id, owner_id, created_at);
CREATE INDEX IF NOT EXISTS ix_analysis_requests_correlation ON analysis.analysis_requests (correlation_id);

-- 2. images (analysis theo tung anh). id = analysisId
CREATE TABLE IF NOT EXISTS analysis.images (
    id                    uuid          NOT NULL,
    tenant_id             uuid          NOT NULL,
    analysis_request_id   uuid          NOT NULL,
    file_id               uuid          NOT NULL,
    modality              varchar(16)   NOT NULL,
    eye_side              varchar(16)   NOT NULL,
    object_key            text          NOT NULL,
    mime_type             varchar(100)  NOT NULL,
    size_bytes            bigint        NOT NULL,
    width                 integer       NOT NULL,
    height                integer       NOT NULL,
    sha256                varchar(64)   NOT NULL,
    processing_status     varchar(32)   NOT NULL,
    review_status         varchar(32)   NOT NULL DEFAULT 'NOT_REQUIRED',
    existing_analysis_id  uuid          NULL,
    created_at            timestamptz   NOT NULL DEFAULT now(),
    updated_at            timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT pk_images PRIMARY KEY (id),
    CONSTRAINT uq_images_file_id UNIQUE (file_id),
    CONSTRAINT uq_images_object_key UNIQUE (object_key),
    CONSTRAINT uq_images_tenant_id UNIQUE (tenant_id, id),
    CONSTRAINT fk_images_request FOREIGN KEY (tenant_id, analysis_request_id)
        REFERENCES analysis.analysis_requests (tenant_id, id),
    CONSTRAINT fk_images_existing FOREIGN KEY (tenant_id, existing_analysis_id)
        REFERENCES analysis.images (tenant_id, id),
    CONSTRAINT ck_images_modality CHECK (modality = 'FUNDUS'),
    CONSTRAINT ck_images_eye_side CHECK (eye_side IN ('left', 'right', 'unknown')),
    CONSTRAINT ck_images_mime CHECK (mime_type IN ('image/jpeg', 'image/png')),
    CONSTRAINT ck_images_size CHECK (size_bytes > 0),
    CONSTRAINT ck_images_dimensions CHECK (width > 0 AND height > 0),
    CONSTRAINT ck_images_sha256 CHECK (sha256 ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_images_processing_status CHECK (processing_status IN
        ('PENDING','QUEUED','PROCESSING','COMPLETED','FAILED','RETRY_READY','LOW_QUALITY','REJECTED','DUPLICATE')),
    CONSTRAINT ck_images_review_status CHECK (review_status IN
        ('NOT_REQUIRED','PENDING','IN_REVIEW','REVIEWED','CORRECTION_REQUIRED')),
    CONSTRAINT ck_images_duplicate_ref CHECK (
        (processing_status = 'DUPLICATE') = (existing_analysis_id IS NOT NULL))
);
CREATE INDEX IF NOT EXISTS ix_images_request ON analysis.images (tenant_id, analysis_request_id);
CREATE INDEX IF NOT EXISTS ix_images_processing ON analysis.images (tenant_id, processing_status, created_at);
CREATE INDEX IF NOT EXISTS ix_images_review ON analysis.images (tenant_id, review_status, created_at);
-- Dedupe: tim anh cung tenant + checksum; modality/eye_side/patient duoc loc them qua join batch
CREATE INDEX IF NOT EXISTS ix_images_dedupe ON analysis.images (tenant_id, sha256, modality, eye_side);

-- 3. jobs (moi dong = mot attempt)
CREATE TABLE IF NOT EXISTS analysis.jobs (
    id              uuid         NOT NULL,
    tenant_id       uuid         NOT NULL,
    image_id        uuid         NOT NULL,
    message_id      uuid         NOT NULL,
    attempt_no      integer      NOT NULL,
    status          varchar(32)  NOT NULL,
    retryable       boolean      NOT NULL DEFAULT false,
    error_code      varchar(100) NULL,
    error_details   jsonb        NULL,
    correlation_id  varchar(64)  NOT NULL,
    started_at      timestamptz  NULL,
    finished_at     timestamptz  NULL,
    next_retry_at   timestamptz  NULL,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT pk_jobs PRIMARY KEY (id),
    CONSTRAINT uq_jobs_message_id UNIQUE (message_id),
    CONSTRAINT uq_jobs_image_attempt UNIQUE (image_id, attempt_no),
    CONSTRAINT uq_jobs_tenant_id UNIQUE (tenant_id, id),
    CONSTRAINT fk_jobs_image FOREIGN KEY (tenant_id, image_id) REFERENCES analysis.images (tenant_id, id),
    CONSTRAINT ck_jobs_attempt CHECK (attempt_no >= 1),
    CONSTRAINT ck_jobs_status CHECK (status IN
        ('QUEUED','RUNNING','SUCCEEDED','FAILED','RETRY_READY','DEAD_LETTER')),
    CONSTRAINT ck_jobs_correlation CHECK (length(correlation_id) BETWEEN 1 AND 64)
);
CREATE INDEX IF NOT EXISTS ix_jobs_image_attempt ON analysis.jobs (tenant_id, image_id, attempt_no);
CREATE INDEX IF NOT EXISTS ix_jobs_scheduler ON analysis.jobs (status, next_retry_at);

-- 4. analysis_results (bat bien; moi lan tao version moi)
CREATE TABLE IF NOT EXISTS analysis.analysis_results (
    id                     uuid             NOT NULL,
    tenant_id              uuid             NOT NULL,
    image_id               uuid             NOT NULL,
    job_id                 uuid             NOT NULL,
    version                integer          NOT NULL,
    result_schema_version  varchar(32)      NOT NULL,
    risk_assessment        jsonb            NOT NULL,
    metrics                jsonb            NOT NULL,
    warnings               jsonb            NOT NULL DEFAULT '[]'::jsonb,
    is_mock                boolean          NOT NULL,
    model_version          varchar(100)     NOT NULL,
    config_version         varchar(100)     NOT NULL,
    threshold_version      varchar(100)     NOT NULL,
    processing_time_ms     double precision NOT NULL,
    limitations            text             NOT NULL,
    created_at             timestamptz      NOT NULL DEFAULT now(),
    CONSTRAINT pk_analysis_results PRIMARY KEY (id),
    CONSTRAINT uq_results_image_version UNIQUE (image_id, version),
    CONSTRAINT uq_results_job UNIQUE (job_id),
    CONSTRAINT uq_results_tenant_id UNIQUE (tenant_id, id),
    CONSTRAINT uq_results_tenant_image_id UNIQUE (tenant_id, image_id, id),
    CONSTRAINT fk_results_image FOREIGN KEY (tenant_id, image_id) REFERENCES analysis.images (tenant_id, id),
    CONSTRAINT fk_results_job FOREIGN KEY (tenant_id, job_id) REFERENCES analysis.jobs (tenant_id, id),
    CONSTRAINT ck_results_version CHECK (version >= 1),
    CONSTRAINT ck_results_time CHECK (processing_time_ms >= 0),
    CONSTRAINT ck_results_warnings CHECK (jsonb_typeof(warnings) = 'array')
);
CREATE INDEX IF NOT EXISTS ix_results_image_version ON analysis.analysis_results (tenant_id, image_id, version);

-- 5. outbox_events
CREATE TABLE IF NOT EXISTS analysis.outbox_events (
    id                   uuid         NOT NULL,
    tenant_id            uuid         NOT NULL,
    analysis_request_id  uuid         NOT NULL,
    image_id             uuid         NOT NULL,
    event_type           varchar(100) NOT NULL,
    schema_version       varchar(32)  NOT NULL,
    payload              jsonb        NOT NULL,
    occurred_at          timestamptz  NOT NULL,
    published_at         timestamptz  NULL,
    delivery_attempts    integer      NOT NULL DEFAULT 0,
    next_publish_at      timestamptz  NULL,
    last_error_code      varchar(100) NULL,
    locked_until         timestamptz  NULL,
    CONSTRAINT pk_outbox_events PRIMARY KEY (id),
    CONSTRAINT fk_outbox_request FOREIGN KEY (tenant_id, analysis_request_id)
        REFERENCES analysis.analysis_requests (tenant_id, id),
    CONSTRAINT fk_outbox_image FOREIGN KEY (tenant_id, image_id) REFERENCES analysis.images (tenant_id, id),
    CONSTRAINT ck_outbox_attempts CHECK (delivery_attempts >= 0),
    CONSTRAINT ck_outbox_payload CHECK (jsonb_typeof(payload) = 'object')
);
CREATE INDEX IF NOT EXISTS ix_outbox_unpublished ON analysis.outbox_events (next_publish_at)
    WHERE published_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_outbox_image ON analysis.outbox_events (tenant_id, image_id);

-- 6. annotation_refs
CREATE TABLE IF NOT EXISTS analysis.annotation_refs (
    id                  uuid          NOT NULL,
    tenant_id           uuid          NOT NULL,
    image_id            uuid          NOT NULL,
    analysis_result_id  uuid          NOT NULL,
    file_id             uuid          NOT NULL,
    type                varchar(16)   NOT NULL,
    object_key          text          NOT NULL,
    mime_type           varchar(100)  NOT NULL,
    size_bytes          bigint        NOT NULL,
    width               integer       NOT NULL,
    height              integer       NOT NULL,
    sha256              varchar(64)   NOT NULL,
    created_at          timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT pk_annotation_refs PRIMARY KEY (id),
    CONSTRAINT uq_annotation_file_id UNIQUE (file_id),
    CONSTRAINT uq_annotation_object_key UNIQUE (object_key),
    CONSTRAINT uq_annotation_result_type UNIQUE (analysis_result_id, type),
    CONSTRAINT fk_annotation_result FOREIGN KEY (tenant_id, image_id, analysis_result_id)
        REFERENCES analysis.analysis_results (tenant_id, image_id, id),
    CONSTRAINT ck_annotation_type CHECK (type IN ('MASK', 'OVERLAY')),
    CONSTRAINT ck_annotation_size CHECK (size_bytes > 0),
    CONSTRAINT ck_annotation_dimensions CHECK (width > 0 AND height > 0),
    CONSTRAINT ck_annotation_sha256 CHECK (sha256 ~ '^[0-9a-f]{64}$')
);
CREATE INDEX IF NOT EXISTS ix_annotation_result ON analysis.annotation_refs (tenant_id, image_id, analysis_result_id);

-- 7. doctor_reviews (append-only)
CREATE TABLE IF NOT EXISTS analysis.doctor_reviews (
    id                    uuid         NOT NULL,
    tenant_id             uuid         NOT NULL,
    image_id              uuid         NOT NULL,
    analysis_result_id    uuid         NOT NULL,
    doctor_id             uuid         NOT NULL,
    version               integer      NOT NULL,
    base_version          integer      NOT NULL,
    is_approved           boolean      NOT NULL,
    corrected_risk_level  varchar(16)  NULL,
    reason                text         NULL,
    doctor_notes          text         NULL,
    created_at            timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT pk_doctor_reviews PRIMARY KEY (id),
    CONSTRAINT uq_reviews_image_version UNIQUE (image_id, version),
    CONSTRAINT fk_reviews_result FOREIGN KEY (tenant_id, image_id, analysis_result_id)
        REFERENCES analysis.analysis_results (tenant_id, image_id, id),
    CONSTRAINT ck_reviews_version CHECK (version >= 1 AND base_version >= 0 AND version = base_version + 1),
    CONSTRAINT ck_reviews_level CHECK (corrected_risk_level IS NULL OR corrected_risk_level IN ('LOW','MODERATE','HIGH')),
    CONSTRAINT ck_reviews_correction CHECK (
        (is_approved AND corrected_risk_level IS NULL)
        OR (NOT is_approved AND corrected_risk_level IS NOT NULL AND length(btrim(coalesce(reason, ''))) > 0))
);
CREATE INDEX IF NOT EXISTS ix_reviews_image ON analysis.doctor_reviews (tenant_id, image_id, version);

-- 8. audit_logs (append-only)
CREATE TABLE IF NOT EXISTS analysis.audit_logs (
    id                   uuid         NOT NULL,
    tenant_id            uuid         NOT NULL,
    analysis_request_id  uuid         NULL,
    image_id             uuid         NULL,
    actor_id             uuid         NULL,
    actor_type           varchar(32)  NOT NULL,
    action               varchar(100) NOT NULL,
    target_type          varchar(100) NOT NULL,
    requested_target_id  text         NULL,
    outcome              varchar(16)  NOT NULL,
    correlation_id       varchar(64)  NOT NULL,
    metadata             jsonb        NOT NULL DEFAULT '{}'::jsonb,
    created_at           timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT pk_audit_logs PRIMARY KEY (id),
    CONSTRAINT fk_audit_request FOREIGN KEY (tenant_id, analysis_request_id)
        REFERENCES analysis.analysis_requests (tenant_id, id),
    CONSTRAINT fk_audit_image FOREIGN KEY (tenant_id, image_id) REFERENCES analysis.images (tenant_id, id),
    CONSTRAINT ck_audit_outcome CHECK (outcome IN ('ALLOWED', 'DENIED', 'ERROR'))
);
CREATE INDEX IF NOT EXISTS ix_audit_tenant_time ON analysis.audit_logs (tenant_id, created_at);
CREATE INDEX IF NOT EXISTS ix_audit_actor ON analysis.audit_logs (tenant_id, actor_id, created_at);
CREATE INDEX IF NOT EXISTS ix_audit_correlation ON analysis.audit_logs (correlation_id);

-- Grants cho runtime role (chi khi M1 da tao role aura_analysis_app). Khong cap DDL, khong cap schema khac.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'aura_analysis_app') THEN
        GRANT USAGE ON SCHEMA analysis TO aura_analysis_app;
        GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA analysis TO aura_analysis_app;
        -- Bang append-only: khong cap UPDATE/DELETE
        REVOKE UPDATE, DELETE ON analysis.analysis_results, analysis.doctor_reviews, analysis.audit_logs FROM aura_analysis_app;
    END IF;
END
$$;

COMMIT;
