-- ====================================================================
-- AURA — Migration 003: Doctor Assignments (Sprint 2 - Task W2-M6-02)
-- Specs: docs/requirements/clinic_campaign_use_cases_v0.1.md & srs_v0.1.md
-- ====================================================================

-- 1. TẠO BẢNG DOCTOR ASSIGNMENTS TRONG SCHEMA PROFILE
-- Bảng phân công bác sĩ phụ trách bệnh nhân trong phạm vi phòng khám (Clinic)
CREATE TABLE IF NOT EXISTS profile.doctor_assignments (
    assignment_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    clinic_id UUID NOT NULL REFERENCES profile.clinics(clinic_id) ON DELETE CASCADE,
    doctor_account_id UUID NOT NULL,
    patient_id UUID NOT NULL REFERENCES profile.patients(patient_id) ON DELETE CASCADE,
    status VARCHAR(32) NOT NULL DEFAULT 'ACTIVE', -- ACTIVE, REVOKED, EXPIRED
    assigned_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    revoked_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_clinic_doctor_patient UNIQUE (clinic_id, doctor_account_id, patient_id)
);

-- 2. TẠO INDEXES TỐI ƯU TRUY VẤN
CREATE INDEX IF NOT EXISTS idx_doctor_assignments_doctor ON profile.doctor_assignments(doctor_account_id, status);
CREATE INDEX IF NOT EXISTS idx_doctor_assignments_patient ON profile.doctor_assignments(patient_id, status);
CREATE INDEX IF NOT EXISTS idx_doctor_assignments_clinic ON profile.doctor_assignments(clinic_id, status);

-- 3. CẤP QUYỀN DML CHO USER RUNTIME
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE profile.doctor_assignments TO aura_profile_app;
