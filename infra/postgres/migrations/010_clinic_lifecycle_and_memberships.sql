-- ====================================================================
-- AURA — Migration 010: Clinic Lifecycle and Membership Management (Task W4-M6-01 / SCRUM-358)
-- Specs: docs/superpowers/specs/2026-11-01-w4-m6-01-clinic-lifecycle-and-membership-design.md
-- ====================================================================

BEGIN;

DO $$
BEGIN
    IF current_database() <> 'aura_dev' THEN
        RAISE EXCEPTION 'Run this migration only on aura_dev';
    END IF;
END
$$;

-- 1. BỔ SUNG CÁC TRƯỜNG PHÁP NHÂN CHO BẢNG PROFILE.CLINICS
ALTER TABLE profile.clinics ADD COLUMN IF NOT EXISTS tax_id VARCHAR(50);
ALTER TABLE profile.clinics ADD COLUMN IF NOT EXISTS address TEXT;
ALTER TABLE profile.clinics ADD COLUMN IF NOT EXISTS representative_name VARCHAR(100);
ALTER TABLE profile.clinics ADD COLUMN IF NOT EXISTS phone VARCHAR(30);
ALTER TABLE profile.clinics ADD COLUMN IF NOT EXISTS audit_reason TEXT;
ALTER TABLE profile.clinics ALTER COLUMN status SET DEFAULT 'PENDING';

-- 2. BẢNG NHẬT KÝ KIỂM TOÁN TRẠNG THÁI PHÒNG KHÁM (CLINIC AUDIT LOGS)
CREATE TABLE IF NOT EXISTS profile.clinic_audit_logs (
    log_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    clinic_id UUID NOT NULL REFERENCES profile.clinics(clinic_id) ON DELETE CASCADE,
    previous_status VARCHAR(32) NOT NULL,
    new_status VARCHAR(32) NOT NULL,
    reason TEXT NULL,
    changed_by UUID NOT NULL,
    changed_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_clinic_audit_logs_clinic ON profile.clinic_audit_logs(clinic_id);

-- 3. CẬP NHẬT PROFILE.PROFILES LƯU EMAIL PHỤC VỤ AUTO-LINK
ALTER TABLE profile.profiles ADD COLUMN IF NOT EXISTS email VARCHAR(255);
CREATE INDEX IF NOT EXISTS idx_profiles_email ON profile.profiles(email);

-- 4. CẬP NHẬT PROFILE.MEMBERSHIPS HỖ TRỢ LỜI MỜI EMAIL
ALTER TABLE profile.memberships ADD COLUMN IF NOT EXISTS invited_email VARCHAR(255);
ALTER TABLE profile.memberships ALTER COLUMN account_id DROP NOT NULL;
CREATE INDEX IF NOT EXISTS idx_memberships_invited_email ON profile.memberships(invited_email);

-- 5. CẤP QUYỀN DML CHO USER RUNTIME PROFILE
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA profile TO aura_profile_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA profile TO aura_profile_app;

COMMIT;
