-- ====================================================================
-- AURA — Test Fixtures: Two Independent Clinics for Tenant Isolation
-- Task: W3-M6-01 (Sprint 3)
-- Specs: docs/superpowers/specs/2026-10-29-w3-m6-01-clinic-membership-design.md
-- ====================================================================

BEGIN;

-- 1. CLINIC ALPHA (Tenant A)
INSERT INTO profile.tenants (tenant_id, tenant_kind, owner_principal_id, created_at)
VALUES ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'CLINIC', '11111111-1111-1111-1111-111111111111', NOW())
ON CONFLICT (tenant_id) DO NOTHING;

INSERT INTO profile.clinics (clinic_id, tenant_id, name, status, created_at, updated_at)
VALUES ('11111111-1111-1111-1111-111111111111', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'Phòng khám Mắt Alpha', 'ACTIVE', NOW(), NOW())
ON CONFLICT (clinic_id) DO NOTHING;

-- Admin Alpha
INSERT INTO identity.accounts (account_id, firebase_uid, account_status, profile_provisioning_status, created_at, updated_at)
VALUES ('a1111111-0000-0000-0000-000000000001', 'firebase_admin_alpha', 'ACTIVE', 'READY', NOW(), NOW())
ON CONFLICT (account_id) DO NOTHING;

INSERT INTO identity.account_roles (account_id, role_name, granted_at)
VALUES ('a1111111-0000-0000-0000-000000000001', 'CLINIC_ADMIN', NOW())
ON CONFLICT (account_id, role_name) DO NOTHING;

INSERT INTO profile.profiles (account_id, display_name, phone, profile_version, created_at, updated_at)
VALUES ('a1111111-0000-0000-0000-000000000001', 'BS. Trưởng Alpha', '0901111001', 1, NOW(), NOW())
ON CONFLICT (account_id) DO NOTHING;

INSERT INTO profile.memberships (membership_id, clinic_id, account_id, role, status, created_at, updated_at)
VALUES ('m1111111-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', 'a1111111-0000-0000-0000-000000000001', 'CLINIC_ADMIN', 'ACTIVE', NOW(), NOW())
ON CONFLICT (clinic_id, account_id) DO NOTHING;

-- Doctor Alpha
INSERT INTO identity.accounts (account_id, firebase_uid, account_status, profile_provisioning_status, created_at, updated_at)
VALUES ('a1111111-0000-0000-0000-000000000002', 'firebase_doctor_alpha', 'ACTIVE', 'READY', NOW(), NOW())
ON CONFLICT (account_id) DO NOTHING;

INSERT INTO identity.account_roles (account_id, role_name, granted_at)
VALUES ('a1111111-0000-0000-0000-000000000002', 'DOCTOR', NOW())
ON CONFLICT (account_id, role_name) DO NOTHING;

INSERT INTO profile.profiles (account_id, display_name, phone, profile_version, created_at, updated_at)
VALUES ('a1111111-0000-0000-0000-000000000002', 'BS. Nhãn khoa Alpha', '0901111002', 1, NOW(), NOW())
ON CONFLICT (account_id) DO NOTHING;

INSERT INTO profile.memberships (membership_id, clinic_id, account_id, role, status, created_at, updated_at)
VALUES ('m1111111-0000-0000-0000-000000000002', '11111111-1111-1111-1111-111111111111', 'a1111111-0000-0000-0000-000000000002', 'DOCTOR', 'ACTIVE', NOW(), NOW())
ON CONFLICT (clinic_id, account_id) DO NOTHING;


-- 2. CLINIC BETA (Tenant B)
INSERT INTO profile.tenants (tenant_id, tenant_kind, owner_principal_id, created_at)
VALUES ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'CLINIC', '22222222-2222-2222-2222-222222222222', NOW())
ON CONFLICT (tenant_id) DO NOTHING;

INSERT INTO profile.clinics (clinic_id, tenant_id, name, status, created_at, updated_at)
VALUES ('22222222-2222-2222-2222-222222222222', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'Phòng khám Mắt Beta', 'ACTIVE', NOW(), NOW())
ON CONFLICT (clinic_id) DO NOTHING;

-- Admin Beta
INSERT INTO identity.accounts (account_id, firebase_uid, account_status, profile_provisioning_status, created_at, updated_at)
VALUES ('b2222222-0000-0000-0000-000000000001', 'firebase_admin_beta', 'ACTIVE', 'READY', NOW(), NOW())
ON CONFLICT (account_id) DO NOTHING;

INSERT INTO identity.account_roles (account_id, role_name, granted_at)
VALUES ('b2222222-0000-0000-0000-000000000001', 'CLINIC_ADMIN', NOW())
ON CONFLICT (account_id, role_name) DO NOTHING;

INSERT INTO profile.profiles (account_id, display_name, phone, profile_version, created_at, updated_at)
VALUES ('b2222222-0000-0000-0000-000000000001', 'BS. Trưởng Beta', '0902222001', 1, NOW(), NOW())
ON CONFLICT (account_id) DO NOTHING;

INSERT INTO profile.memberships (membership_id, clinic_id, account_id, role, status, created_at, updated_at)
VALUES ('m2222222-0000-0000-0000-000000000001', '22222222-2222-2222-2222-222222222222', 'b2222222-0000-0000-0000-000000000001', 'CLINIC_ADMIN', 'ACTIVE', NOW(), NOW())
ON CONFLICT (clinic_id, account_id) DO NOTHING;

-- Doctor Beta
INSERT INTO identity.accounts (account_id, firebase_uid, account_status, profile_provisioning_status, created_at, updated_at)
VALUES ('b2222222-0000-0000-0000-000000000002', 'firebase_doctor_beta', 'ACTIVE', 'READY', NOW(), NOW())
ON CONFLICT (account_id) DO NOTHING;

INSERT INTO identity.account_roles (account_id, role_name, granted_at)
VALUES ('b2222222-0000-0000-0000-000000000002', 'DOCTOR', NOW())
ON CONFLICT (account_id, role_name) DO NOTHING;

INSERT INTO profile.profiles (account_id, display_name, phone, profile_version, created_at, updated_at)
VALUES ('b2222222-0000-0000-0000-000000000002', 'BS. Nhãn khoa Beta', '0902222002', 1, NOW(), NOW())
ON CONFLICT (account_id) DO NOTHING;

INSERT INTO profile.memberships (membership_id, clinic_id, account_id, role, status, created_at, updated_at)
VALUES ('m2222222-0000-0000-0000-000000000002', '22222222-2222-2222-2222-222222222222', 'b2222222-0000-0000-0000-000000000002', 'DOCTOR', 'ACTIVE', NOW(), NOW())
ON CONFLICT (clinic_id, account_id) DO NOTHING;


-- 3. FREE USER (Dùng để kiểm thử thêm/mời thành viên vào Clinic)
INSERT INTO identity.accounts (account_id, firebase_uid, account_status, profile_provisioning_status, created_at, updated_at)
VALUES ('f0000000-0000-0000-0000-000000000001', 'firebase_free_user', 'ACTIVE', 'READY', NOW(), NOW())
ON CONFLICT (account_id) DO NOTHING;

INSERT INTO identity.account_roles (account_id, role_name, granted_at)
VALUES ('f0000000-0000-0000-0000-000000000001', 'DOCTOR', NOW())
ON CONFLICT (account_id, role_name) DO NOTHING;

INSERT INTO profile.profiles (account_id, display_name, phone, profile_version, created_at, updated_at)
VALUES ('f0000000-0000-0000-0000-000000000001', 'Bác sĩ Tự do Chưa Có Phòng Khám', '0909999001', 1, NOW(), NOW())
ON CONFLICT (account_id) DO NOTHING;

COMMIT;
