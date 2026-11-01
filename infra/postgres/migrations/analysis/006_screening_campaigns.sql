-- W4-M6-03 / SCRUM-378 / SCRUM-360: Screening Campaign Module Migration
-- Supports screening campaigns, batch linking, progress tracking and status lifecycle

BEGIN;

CREATE TABLE IF NOT EXISTS analysis.campaigns (
    id                      uuid         NOT NULL,
    tenant_id               uuid         NOT NULL,
    name                    varchar(255) NOT NULL,
    description             text         NULL,
    location                varchar(255) NOT NULL,
    doctor_in_charge_id     uuid         NULL,
    target_patient_count    integer      NOT NULL DEFAULT 0,
    status                  varchar(32)  NOT NULL DEFAULT 'PLANNED',
    start_date              timestamptz  NOT NULL,
    end_date                timestamptz  NOT NULL,
    created_at              timestamptz  NOT NULL DEFAULT now(),
    updated_at              timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT pk_campaigns PRIMARY KEY (id),
    CONSTRAINT uq_campaigns_tenant_id UNIQUE (tenant_id, id),
    CONSTRAINT ck_campaigns_status CHECK (status IN ('PLANNED', 'IN_PROGRESS', 'COMPLETED', 'CLOSED')),
    CONSTRAINT ck_campaigns_dates CHECK (end_date >= start_date),
    CONSTRAINT ck_campaigns_target CHECK (target_patient_count >= 0)
);

CREATE INDEX IF NOT EXISTS ix_campaigns_tenant_status ON analysis.campaigns (tenant_id, status);
CREATE INDEX IF NOT EXISTS ix_campaigns_tenant_dates  ON analysis.campaigns (tenant_id, start_date, end_date);

-- Link analysis.batches to analysis.campaigns
ALTER TABLE analysis.batches
    ADD COLUMN IF NOT EXISTS campaign_id uuid NULL;

CREATE INDEX IF NOT EXISTS ix_batches_campaign ON analysis.batches (tenant_id, campaign_id);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'fk_batches_campaign' AND conrelid = 'analysis.batches'::regclass
    ) THEN
        ALTER TABLE analysis.batches
            ADD CONSTRAINT fk_batches_campaign FOREIGN KEY (tenant_id, campaign_id)
            REFERENCES analysis.campaigns (tenant_id, id) ON DELETE SET NULL;
    END IF;
END $$;

-- Permissions
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'aura_analysis_app') THEN
        GRANT SELECT, INSERT, UPDATE ON analysis.campaigns TO aura_analysis_app;
        GRANT SELECT, UPDATE (campaign_id) ON analysis.batches TO aura_analysis_app;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'aura_analysis_migrator') THEN
        GRANT ALL PRIVILEGES ON analysis.campaigns TO aura_analysis_migrator;
    END IF;
END $$;

COMMIT;
