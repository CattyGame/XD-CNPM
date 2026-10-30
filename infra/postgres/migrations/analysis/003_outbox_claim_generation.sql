-- Candidate84 only. Apply only in a separately reviewed migration/deployment plan.
-- Requires existing 001 core and 002 outbox migration. Never mix old/new publishers.
BEGIN;
ALTER TABLE analysis.outbox_events
    ADD COLUMN IF NOT EXISTS claim_generation bigint NOT NULL DEFAULT 0;
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='ck_outbox_claim_generation'
        AND conrelid='analysis.outbox_events'::regclass) THEN
        ALTER TABLE analysis.outbox_events ADD CONSTRAINT ck_outbox_claim_generation CHECK (claim_generation >= 0);
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname='aura_analysis_publisher') THEN
        GRANT UPDATE (claim_generation) ON analysis.outbox_events TO aura_analysis_publisher;
    END IF;
END $$;
-- Generation increases on every claim, including after ReleaseUnattempted.
-- It must never be reset during retries, operator replay or lease renewal.
COMMIT;
