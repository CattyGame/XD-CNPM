BEGIN;

-- One optimistic-concurrency version per analysis image. This is independent
-- of analysis_results.version, which remains immutable and AI-owned.
ALTER TABLE analysis.images
    ADD COLUMN IF NOT EXISTS row_version integer NOT NULL DEFAULT 0;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'ck_images_row_version' AND conrelid = 'analysis.images'::regclass
    ) THEN
        ALTER TABLE analysis.images
            ADD CONSTRAINT ck_images_row_version CHECK (row_version >= 0);
    END IF;
END
$$;

-- Recover the current revision number when upgrading a database that already
-- contains append-only doctor_reviews.
UPDATE analysis.images i
SET row_version = COALESCE((
    SELECT MAX(r.version)
    FROM analysis.doctor_reviews r
    WHERE r.tenant_id = i.tenant_id AND r.image_id = i.id
), 0);

CREATE INDEX IF NOT EXISTS ix_images_doctor_worklist
    ON analysis.images (tenant_id, created_at)
    WHERE processing_status = 'COMPLETED' AND review_status = 'PENDING';

COMMIT;
