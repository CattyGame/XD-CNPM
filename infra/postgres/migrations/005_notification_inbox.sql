-- ====================================================================
-- AURA Sprint 3 — Notification Inbox Schema (SCRUM-327 / W3-M3-02)
-- Durable inbox items table with deduplication and unread indexing.
-- ====================================================================

BEGIN;

DO $$
BEGIN
    IF current_database() <> 'aura_dev' THEN
        RAISE EXCEPTION 'Run this migration only on aura_dev';
    END IF;
END
$$;

SET LOCAL ROLE aura_notification_migrator;

CREATE TABLE IF NOT EXISTS notification.inbox_items (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    event_id uuid NOT NULL,
    analysis_id uuid NOT NULL,
    type varchar(32) NOT NULL
        CHECK (type IN ('ANALYSIS_COMPLETED', 'ANALYSIS_FAILED')),
    title varchar(255) NOT NULL,
    is_read boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT now(),
    read_at timestamptz,
    target_path varchar(128) NOT NULL,
    metadata jsonb NOT NULL DEFAULT '{}'::jsonb,
    
    -- AC1: Chống event trùng bằng khóa dedup (account_id, event_id)
    CONSTRAINT uq_notification_inbox_dedup
        UNIQUE (account_id, event_id),
        
    -- Tính nhất quán giữa trạng thái đọc và thời điểm đọc
    CONSTRAINT ck_notification_inbox_read_state
        CHECK (
            (NOT is_read AND read_at IS NULL)
            OR
            (is_read AND read_at IS NOT NULL)
        )
);

-- Chỉ mục phân trang ổn định theo tài khoản và thời gian
CREATE INDEX IF NOT EXISTS ix_notification_inbox_account_created
    ON notification.inbox_items (account_id, created_at DESC, id DESC);

-- Chỉ mục đếm số lượng chưa đọc nhanh chóng
CREATE INDEX IF NOT EXISTS ix_notification_inbox_unread
    ON notification.inbox_items (account_id)
    WHERE is_read = false;

COMMENT ON TABLE notification.inbox_items IS
    'Durable notification inbox items for web and mobile users with event deduplication.';

-- Cấp quyền cho runtime role aura_notification_app
GRANT SELECT, INSERT, UPDATE (is_read, read_at)
    ON notification.inbox_items TO aura_notification_app;

COMMIT;
