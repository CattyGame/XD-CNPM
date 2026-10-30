-- ====================================================================
-- AURA — Test Fixtures: Camera Delegation and Doctor Assignments
-- Task: W3-M6-02 (Sprint 3)
-- Specs: docs/superpowers/specs/2026-10-30-w3-m6-02-patient-doctor-camera-authorization-design.md
-- ====================================================================

BEGIN;

-- 1. TÀI KHOẢN VÀ BỆNH NHÂN CÁ NHÂN (PERSONAL TENANT)
INSERT INTO profile.tenants (tenant_id, tenant_kind, owner_principal_id, created_at)
VALUES ('t1111111-1111-4111-8111-111111111111', 'PERSONAL', 'a1111111-1111-4111-8111-111111111111', NOW())
ON CONFLICT (tenant_id) DO NOTHING;

-- Tài khoản cá nhân hợp lệ (ACTIVE)
INSERT INTO identity.accounts (account_id, firebase_uid, account_status, profile_provisioning_status, created_at, updated_at)
VALUES ('a1111111-1111-4111-8111-111111111111', 'firebase_personal_user_1', 'ACTIVE', 'READY', NOW(), NOW())
ON CONFLICT (account_id) DO NOTHING;

INSERT INTO identity.account_roles (account_id, role_name, granted_at)
VALUES ('a1111111-1111-4111-8111-111111111111', 'PATIENT', NOW())
ON CONFLICT (account_id, role_name) DO NOTHING;

INSERT INTO profile.profiles (account_id, display_name, phone, personal_tenant_id, profile_version, created_at, updated_at)
VALUES ('a1111111-1111-4111-8111-111111111111', 'Bệnh nhân Cá nhân 1', '0911111111', 't1111111-1111-4111-8111-111111111111', 1, NOW(), NOW())
ON CONFLICT (account_id) DO NOTHING;

INSERT INTO profile.patients (patient_id, tenant_id, linked_account_id, owner_principal_id, status, created_at, updated_at)
VALUES ('p1111111-1111-4111-8111-111111111111', 't1111111-1111-4111-8111-111111111111', 'a1111111-1111-4111-8111-111111111111', 'a1111111-1111-4111-8111-111111111111', 'ACTIVE', NOW(), NOW())
ON CONFLICT (patient_id) DO NOTHING;

-- Tài khoản cá nhân bị khóa (DISABLED)
INSERT INTO identity.accounts (account_id, firebase_uid, account_status, profile_provisioning_status, created_at, updated_at)
VALUES ('a2222222-2222-4222-8222-222222222222', 'firebase_personal_user_disabled', 'DISABLED', 'READY', NOW(), NOW())
ON CONFLICT (account_id) DO NOTHING;

INSERT INTO identity.account_roles (account_id, role_name, granted_at)
VALUES ('a2222222-2222-4222-8222-222222222222', 'PATIENT', NOW())
ON CONFLICT (account_id, role_name) DO NOTHING;

INSERT INTO profile.profiles (account_id, display_name, phone, profile_version, created_at, updated_at)
VALUES ('a2222222-2222-4222-8222-222222222222', 'Bệnh nhân Bị Khóa', '0922222222', 1, NOW(), NOW())
ON CONFLICT (account_id) DO NOTHING;

-- 2. CAMERA DELEGATION GRANTS
-- Grant 1: Hợp lệ, ACTIVE, chưa hết hạn (device: fundus-cam-01)
INSERT INTO profile.camera_grants (grant_id, service_principal_id, client_id, device_id, patient_id, tenant_id, status, expires_at, created_at, updated_at)
VALUES (
    'g1111111-1111-4111-8111-111111111111',
    '6991e61d-15d2-5107-86c0-dd3966af169c',
    'nifi-camera',
    'fundus-cam-01',
    'p1111111-1111-4111-8111-111111111111',
    't1111111-1111-4111-8111-111111111111',
    'ACTIVE',
    NOW() + INTERVAL '30 days',
    NOW(),
    NOW()
)
ON CONFLICT (device_id, patient_id) DO NOTHING;

-- Grant 2: Đã hết hạn (device: fundus-cam-expired)
INSERT INTO profile.camera_grants (grant_id, service_principal_id, client_id, device_id, patient_id, tenant_id, status, expires_at, created_at, updated_at)
VALUES (
    'g2222222-2222-4222-8222-222222222222',
    '6991e61d-15d2-5107-86c0-dd3966af169c',
    'nifi-camera',
    'fundus-cam-expired',
    'p1111111-1111-4111-8111-111111111111',
    't1111111-1111-4111-8111-111111111111',
    'ACTIVE',
    NOW() - INTERVAL '1 day',
    NOW(),
    NOW()
)
ON CONFLICT (device_id, patient_id) DO NOTHING;

-- Grant 3: Đã bị thu hồi (device: fundus-cam-revoked)
INSERT INTO profile.camera_grants (grant_id, service_principal_id, client_id, device_id, patient_id, tenant_id, status, expires_at, created_at, updated_at)
VALUES (
    'g3333333-3333-4333-8333-333333333333',
    '6991e61d-15d2-5107-86c0-dd3966af169c',
    'nifi-camera',
    'fundus-cam-revoked',
    'p1111111-1111-4111-8111-111111111111',
    't1111111-1111-4111-8111-111111111111',
    'REVOKED',
    NOW() + INTERVAL '30 days',
    NOW(),
    NOW()
)
ON CONFLICT (device_id, patient_id) DO NOTHING;

-- 3. CLINIC VÀ PHÂN CÔNG BÁC SĨ (DOCTOR ASSIGNMENTS)
-- Tenant & Clinic Gamma
INSERT INTO profile.tenants (tenant_id, tenant_kind, owner_principal_id, created_at)
VALUES ('t3333333-3333-4333-8333-333333333333', 'CLINIC', 'c3333333-3333-4333-8333-333333333333', NOW())
ON CONFLICT (tenant_id) DO NOTHING;

INSERT INTO profile.clinics (clinic_id, tenant_id, name, status, created_at, updated_at)
VALUES ('c3333333-3333-4333-8333-333333333333', 't3333333-3333-4333-8333-333333333333', 'Phòng khám Mắt Gamma', 'ACTIVE', NOW(), NOW())
ON CONFLICT (clinic_id) DO NOTHING;

-- Clinic Admin Gamma
INSERT INTO identity.accounts (account_id, firebase_uid, account_status, profile_provisioning_status, created_at, updated_at)
VALUES ('a3333333-3333-4333-8333-333333333333', 'firebase_admin_gamma', 'ACTIVE', 'READY', NOW(), NOW())
ON CONFLICT (account_id) DO NOTHING;

INSERT INTO identity.account_roles (account_id, role_name, granted_at)
VALUES ('a3333333-3333-4333-8333-333333333333', 'CLINIC_ADMIN', NOW())
ON CONFLICT (account_id, role_name) DO NOTHING;

INSERT INTO profile.profiles (account_id, display_name, phone, profile_version, created_at, updated_at)
VALUES ('a3333333-3333-4333-8333-333333333333', 'Admin Gamma', '0933333333', 1, NOW(), NOW())
ON CONFLICT (account_id) DO NOTHING;

INSERT INTO profile.memberships (membership_id, clinic_id, account_id, role, status, created_at, updated_at)
VALUES ('m3333333-3333-4333-8333-333333333333', 'c3333333-3333-4333-8333-333333333333', 'a3333333-3333-4333-8333-333333333333', 'CLINIC_ADMIN', 'ACTIVE', NOW(), NOW())
ON CONFLICT (clinic_id, account_id) DO NOTHING;

-- Bác sĩ Gamma 1 (ACTIVE)
INSERT INTO identity.accounts (account_id, firebase_uid, account_status, profile_provisioning_status, created_at, updated_at)
VALUES ('a4444444-4444-4444-8444-444444444444', 'firebase_doctor_gamma_1', 'ACTIVE', 'READY', NOW(), NOW())
ON CONFLICT (account_id) DO NOTHING;

INSERT INTO identity.account_roles (account_id, role_name, granted_at)
VALUES ('a4444444-4444-4444-8444-444444444444', 'DOCTOR', NOW())
ON CONFLICT (account_id, role_name) DO NOTHING;

INSERT INTO profile.profiles (account_id, display_name, phone, profile_version, created_at, updated_at)
VALUES ('a4444444-4444-4444-8444-444444444444', 'BS. Le Van Doctor', '0944444444', 1, NOW(), NOW())
ON CONFLICT (account_id) DO NOTHING;

INSERT INTO profile.memberships (membership_id, clinic_id, account_id, role, status, created_at, updated_at)
VALUES ('m4444444-4444-4444-8444-444444444444', 'c3333333-3333-4333-8333-333333333333', 'a4444444-4444-4444-8444-444444444444', 'DOCTOR', 'ACTIVE', NOW(), NOW())
ON CONFLICT (clinic_id, account_id) DO NOTHING;

-- Bác sĩ Gamma 2 (DISABLED account)
INSERT INTO identity.accounts (account_id, firebase_uid, account_status, profile_provisioning_status, created_at, updated_at)
VALUES ('a5555555-5555-4555-8555-555555555555', 'firebase_doctor_gamma_disabled', 'DISABLED', 'READY', NOW(), NOW())
ON CONFLICT (account_id) DO NOTHING;

INSERT INTO identity.account_roles (account_id, role_name, granted_at)
VALUES ('a5555555-5555-4555-8555-555555555555', 'DOCTOR', NOW())
ON CONFLICT (account_id, role_name) DO NOTHING;

INSERT INTO profile.profiles (account_id, display_name, phone, profile_version, created_at, updated_at)
VALUES ('a5555555-5555-4555-8555-555555555555', 'BS. Bi Khoa Tai Khoan', '0955555555', 1, NOW(), NOW())
ON CONFLICT (account_id) DO NOTHING;

INSERT INTO profile.memberships (membership_id, clinic_id, account_id, role, status, created_at, updated_at)
VALUES ('m5555555-5555-4555-8555-555555555555', 'c3333333-3333-4333-8333-333333333333', 'a5555555-5555-4555-8555-555555555555', 'DOCTOR', 'ACTIVE', NOW(), NOW())
ON CONFLICT (clinic_id, account_id) DO NOTHING;

-- Bệnh nhân của Clinic Gamma
INSERT INTO profile.patients (patient_id, tenant_id, linked_account_id, owner_principal_id, status, created_at, updated_at)
VALUES ('p3333333-3333-4333-8333-333333333333', 't3333333-3333-4333-8333-333333333333', NULL, 'c3333333-3333-4333-8333-333333333333', 'ACTIVE', NOW(), NOW())
ON CONFLICT (patient_id) DO NOTHING;

INSERT INTO profile.patients (patient_id, tenant_id, linked_account_id, owner_principal_id, status, created_at, updated_at)
VALUES ('p4444444-4444-4444-8444-444444444444', 't3333333-3333-4333-8333-333333333333', NULL, 'c3333333-3333-4333-8333-333333333333', 'ACTIVE', NOW(), NOW())
ON CONFLICT (patient_id) DO NOTHING;

-- Phân công 1: ACTIVE (Doctor 1 cho Patient 1 tại Clinic Gamma)
INSERT INTO profile.doctor_assignments (assignment_id, clinic_id, doctor_account_id, patient_id, status, assigned_at, created_at, updated_at)
VALUES (
    's1111111-1111-4111-8111-111111111111',
    'c3333333-3333-4333-8333-333333333333',
    'a4444444-4444-4444-8444-444444444444',
    'p3333333-3333-4333-8333-333333333333',
    'ACTIVE',
    NOW(),
    NOW(),
    NOW()
)
ON CONFLICT (clinic_id, doctor_account_id, patient_id) DO NOTHING;

-- Phân công 2: REVOKED (Doctor 1 cho Patient 2 tại Clinic Gamma)
INSERT INTO profile.doctor_assignments (assignment_id, clinic_id, doctor_account_id, patient_id, status, assigned_at, revoked_at, created_at, updated_at)
VALUES (
    's2222222-2222-4222-8222-222222222222',
    'c3333333-3333-4333-8333-333333333333',
    'a4444444-4444-4444-8444-444444444444',
    'p4444444-4444-4444-8444-444444444444',
    'REVOKED',
    NOW() - INTERVAL '5 days',
    NOW() - INTERVAL '1 day',
    NOW() - INTERVAL '5 days',
    NOW() - INTERVAL '1 day'
)
ON CONFLICT (clinic_id, doctor_account_id, patient_id) DO NOTHING;

-- Clinic Delta (Độc lập cho kiểm thử chéo phòng khám)
INSERT INTO profile.tenants (tenant_id, tenant_kind, owner_principal_id, created_at)
VALUES ('t6666666-6666-4666-8666-666666666666', 'CLINIC', 'c6666666-6666-4666-8666-666666666666', NOW())
ON CONFLICT (tenant_id) DO NOTHING;

INSERT INTO profile.clinics (clinic_id, tenant_id, name, status, created_at, updated_at)
VALUES ('c6666666-6666-4666-8666-666666666666', 't6666666-6666-4666-8666-666666666666', 'Phòng khám Mắt Delta', 'ACTIVE', NOW(), NOW())
ON CONFLICT (clinic_id) DO NOTHING;

COMMIT;
