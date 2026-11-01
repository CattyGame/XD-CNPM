-- ====================================================================
-- AURA — Migration 005: Camera Delegation Grants & Account Access (Task W3-M6-02)
-- Specs: docs/superpowers/specs/2026-10-30-w3-m6-02-patient-doctor-camera-authorization-design.md
-- ====================================================================

BEGIN;

DO $$
BEGIN
    IF current_database() <> 'aura_dev' THEN
        RAISE EXCEPTION 'Run this migration only on aura_dev';
    END IF;
END
$$;

-- 1. BẢNG CAMERA DELEGATION GRANTS TRONG SCHEMA PROFILE
-- Quản lý ủy quyền thiết bị camera chụp đáy mắt gửi ảnh cho bệnh nhân
CREATE TABLE IF NOT EXISTS profile.camera_grants (
    grant_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    service_principal_id UUID NOT NULL,
    client_id VARCHAR(64) NOT NULL,
    device_id VARCHAR(128) NOT NULL,
    patient_id UUID NOT NULL REFERENCES profile.patients(patient_id) ON DELETE CASCADE,
    tenant_id UUID NOT NULL REFERENCES profile.tenants(tenant_id) ON DELETE CASCADE,
    status VARCHAR(32) NOT NULL DEFAULT 'ACTIVE', -- ACTIVE, REVOKED, EXPIRED
    expires_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_camera_grant_device_patient UNIQUE (device_id, patient_id)
);

-- 2. TẠO INDEXES TỐI ƯU TRUY VẤN XÁC THỰC CAMERA
CREATE INDEX IF NOT EXISTS idx_camera_grants_lookup
    ON profile.camera_grants(device_id, client_id, status);

CREATE INDEX IF NOT EXISTS idx_camera_grants_patient
    ON profile.camera_grants(patient_id, status);

-- 3. CẤP QUYỀN DML CHO USER RUNTIME PROFILE
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE profile.camera_grants TO aura_profile_app;

-- 4. CẤP QUYỀN ĐỌC AN TOÀN TRẠNG THÁI TÀI KHOẢN IDENTITY CHO PROFILE RUNTIME
-- Cho phép Profile Service kiểm tra account_status trực tiếp (fail-closed)
GRANT USAGE ON SCHEMA identity TO aura_profile_app;
GRANT SELECT ON TABLE identity.accounts TO aura_profile_app;

COMMIT;
