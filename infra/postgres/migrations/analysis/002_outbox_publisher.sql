-- Sprint 3 / W3-M2-01: bo sung trang thai NEEDS_ATTENTION, thoi diem loi, view theo doi outbox.
-- Chay SAU 001_analysis_core.sql. Idempotent. Khong xoa/doi ten cot cu.
BEGIN;

ALTER TABLE analysis.outbox_events ADD COLUMN IF NOT EXISTS needs_attention_at timestamptz NULL;
ALTER TABLE analysis.outbox_events ADD COLUMN IF NOT EXISTS last_error_at      timestamptz NULL;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_outbox_final_state'
                   AND conrelid = 'analysis.outbox_events'::regclass) THEN
        ALTER TABLE analysis.outbox_events ADD CONSTRAINT ck_outbox_final_state
            CHECK (NOT (published_at IS NOT NULL AND needs_attention_at IS NOT NULL));
    END IF;
END $$;

-- Index cho claim: record chua published, chua NEEDS_ATTENTION (giu ix_outbox_unpublished cu cua 001).
CREATE INDEX IF NOT EXISTS ix_outbox_claimable ON analysis.outbox_events (next_publish_at, occurred_at)
    WHERE published_at IS NULL AND needs_attention_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_outbox_attention ON analysis.outbox_events (needs_attention_at)
    WHERE needs_attention_at IS NOT NULL AND published_at IS NULL;

-- Theo doi backlog va tra cuu theo correlationId (khong co payload, khong co thong tin benh nhan).
CREATE OR REPLACE VIEW analysis.v_outbox_status AS
SELECT id, tenant_id, event_type, payload->>'correlationId' AS correlation_id,
       occurred_at, delivery_attempts, next_publish_at, locked_until,
       published_at, needs_attention_at, last_error_code, last_error_at,
       CASE WHEN published_at IS NOT NULL THEN 'PUBLISHED'
            WHEN needs_attention_at IS NOT NULL THEN 'NEEDS_ATTENTION'
            WHEN locked_until > now() THEN 'IN_FLIGHT'
            WHEN next_publish_at > now() THEN 'RETRY_WAIT'
            ELSE 'PENDING' END AS state
FROM analysis.outbox_events;

-- Quyen toi thieu cho publisher (role do M1 tao; thieu role thi bo qua, giong 001).
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'aura_analysis_publisher') THEN
        GRANT USAGE ON SCHEMA analysis TO aura_analysis_publisher;
        GRANT SELECT ON analysis.outbox_events, analysis.v_outbox_status TO aura_analysis_publisher;
        GRANT UPDATE (published_at, delivery_attempts, next_publish_at, last_error_code,
                      locked_until, needs_attention_at, last_error_at)
            ON analysis.outbox_events TO aura_analysis_publisher;
    END IF;
END $$;

COMMIT;
