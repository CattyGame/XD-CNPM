-- ====================================================================
-- AURA — Migration 002: Identity & Profile Services (Sprint 2)
-- Specs: docs/requirements/srs_v0.1.md & Sprint 2 Contract v1.0
-- ====================================================================

-- 1. TẠO SCHEMA
CREATE SCHEMA IF NOT EXISTS identity;
CREATE SCHEMA IF NOT EXISTS profile;

-- 2. TẠO ROLE RUNTIME (Least Privilege)
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'aura_identity_app') THEN
        CREATE ROLE aura_identity_app WITH LOGIN PASSWORD 'aura_identity_pass';
    END IF;
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'aura_profile_app') THEN
        CREATE ROLE aura_profile_app WITH LOGIN PASSWORD 'aura_profile_pass';
    END IF;
END $$;

-- Cấp quyền truy cập schema
GRANT USAGE ON SCHEMA identity TO aura_identity_app;
GRANT USAGE ON SCHEMA profile TO aura_profile_app;

-- ====================================================================
-- SCHEMA IDENTITY
-- ====================================================================

-- Bảng lưu trữ vai trò hệ thống
CREATE TABLE IF NOT EXISTS identity.roles (
    role_name VARCHAR(32) PRIMARY KEY,
    description VARCHAR(255) NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Seed các vai trò chuẩn
INSERT INTO identity.roles (role_name, description) VALUES
    ('PATIENT', 'Bệnh nhân / Người dùng cuối tiêu chuẩn'),
    ('DOCTOR', 'Bác sĩ nhãn khoa / Đánh giá lâm sàng'),
    ('CLINIC_ADMIN', 'Quản trị viên phòng khám / tổ chức y tế'),
    ('SYSTEM_ADMIN', 'Quản trị viên toàn hệ thống')
ON CONFLICT (role_name) DO NOTHING;

-- Bảng tài khoản nội bộ (Mapping 1:1 với Firebase UID)
CREATE TABLE IF NOT EXISTS identity.accounts (
    account_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firebase_uid VARCHAR(128) NOT NULL UNIQUE,
    account_status VARCHAR(32) NOT NULL DEFAULT 'ACTIVE', -- ACTIVE, DISABLED
    profile_provisioning_status VARCHAR(32) NOT NULL DEFAULT 'PENDING', -- PENDING, READY
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_accounts_firebase_uid ON identity.accounts(firebase_uid);

-- Bảng gán vai trò cho tài khoản
CREATE TABLE IF NOT EXISTS identity.account_roles (
    account_id UUID NOT NULL REFERENCES identity.accounts(account_id) ON DELETE CASCADE,
    role_name VARCHAR(32) NOT NULL REFERENCES identity.roles(role_name) ON DELETE RESTRICT,
    granted_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (account_id, role_name)
);

-- Bảng quản lý Service Clients (Dùng cho Client Credentials / Service-to-Service JWT)
CREATE TABLE IF NOT EXISTS identity.service_clients (
    client_id VARCHAR(64) PRIMARY KEY,
    client_secret_hash VARCHAR(256) NOT NULL,
    allowed_scopes TEXT[] NOT NULL DEFAULT '{}',
    allowed_audiences TEXT[] NOT NULL DEFAULT '{}',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Cấp quyền DML trên schema identity cho aura_identity_app
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA identity TO aura_identity_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA identity GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO aura_identity_app;

-- ====================================================================
-- SCHEMA PROFILE
-- ====================================================================

-- Bảng Tenant (Phạm vi phân định dữ liệu)
CREATE TABLE IF NOT EXISTS profile.tenants (
    tenant_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_kind VARCHAR(32) NOT NULL, -- PERSONAL, CLINIC
    owner_principal_id UUID NOT NULL, -- accountId (nếu PERSONAL) hoặc clinicId (nếu CLINIC)
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_tenants_owner ON profile.tenants(owner_principal_id);

-- Bảng Phòng khám / Tổ chức Y tế
CREATE TABLE IF NOT EXISTS profile.clinics (
    clinic_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES profile.tenants(tenant_id) ON DELETE RESTRICT,
    name VARCHAR(200) NOT NULL,
    status VARCHAR(32) NOT NULL DEFAULT 'ACTIVE',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Bảng Hồ sơ người dùng
CREATE TABLE IF NOT EXISTS profile.profiles (
    account_id UUID PRIMARY KEY,
    display_name VARCHAR(100) NOT NULL,
    phone VARCHAR(30) NULL,
    personal_tenant_id UUID NULL REFERENCES profile.tenants(tenant_id) ON DELETE SET NULL,
    profile_version INT NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Bảng Hồ sơ Bệnh nhân (Một bệnh nhân có thể thuộc Personal tenant hoặc Clinic tenant)
CREATE TABLE IF NOT EXISTS profile.patients (
    patient_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES profile.tenants(tenant_id) ON DELETE RESTRICT,
    linked_account_id UUID NULL, -- Có thể NULL nếu bệnh nhân vãng lai do phòng khám nhập
    owner_principal_id UUID NOT NULL, -- accountId (cá nhân) hoặc clinicId (phòng khám)
    status VARCHAR(32) NOT NULL DEFAULT 'ACTIVE',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_patients_tenant ON profile.patients(tenant_id);
CREATE INDEX IF NOT EXISTS idx_patients_linked_account ON profile.patients(linked_account_id);
CREATE INDEX IF NOT EXISTS idx_patients_owner ON profile.patients(owner_principal_id);

-- Bảng Thành viên phòng khám (Bác sĩ, Quản trị viên phòng khám)
CREATE TABLE IF NOT EXISTS profile.memberships (
    membership_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    clinic_id UUID NOT NULL REFERENCES profile.clinics(clinic_id) ON DELETE CASCADE,
    account_id UUID NOT NULL,
    role VARCHAR(32) NOT NULL, -- CLINIC_ADMIN, DOCTOR, STAFF
    status VARCHAR(32) NOT NULL DEFAULT 'ACTIVE',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (clinic_id, account_id)
);

CREATE INDEX IF NOT EXISTS idx_memberships_account ON profile.memberships(account_id);

-- Cấp quyền DML trên schema profile cho aura_profile_app
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA profile TO aura_profile_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA profile GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO aura_profile_app;
