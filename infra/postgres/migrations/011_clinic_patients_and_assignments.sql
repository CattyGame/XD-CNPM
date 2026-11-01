-- ====================================================================
-- AURA — Migration 011: Clinic Patients & Doctor Assignments Extension
-- Task W4-M6-02: Quản lý Bệnh nhân Phòng khám & Phân công Bác sĩ
-- ====================================================================

-- 1. MỞ RỘNG BẢNG PROFILE.PATIENTS (HỒ SƠ BỆNH NHÂN LÂM SÀNG)
ALTER TABLE profile.patients
    ADD COLUMN IF NOT EXISTS patient_type VARCHAR(32) NOT NULL DEFAULT 'CLINIC_MANAGED',
    ADD COLUMN IF NOT EXISTS full_name VARCHAR(255) NULL,
    ADD COLUMN IF NOT EXISTS date_of_birth DATE NULL,
    ADD COLUMN IF NOT EXISTS gender VARCHAR(16) NULL,
    ADD COLUMN IF NOT EXISTS phone VARCHAR(32) NULL,
    ADD COLUMN IF NOT EXISTS health_insurance_number VARCHAR(64) NULL,
    ADD COLUMN IF NOT EXISTS medical_history JSONB NULL;

-- Cập nhật các bệnh nhân cá nhân đã tồn tại sang kiểu PERSONAL
UPDATE profile.patients
SET patient_type = 'PERSONAL'
WHERE linked_account_id IS NOT NULL AND patient_type = 'CLINIC_MANAGED';

-- Tạo index hỗ trợ tìm kiếm bệnh nhân phòng khám theo tên và BHYT
CREATE INDEX IF NOT EXISTS idx_patients_clinic_search
    ON profile.patients(owner_principal_id, status, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_patients_insurance
    ON profile.patients(health_insurance_number);

-- 2. MỞ RỘNG BẢNG PROFILE.DOCTOR_ASSIGNMENTS (PHÂN CÔNG CA CHỤP & CHUYỂN GIAO)
ALTER TABLE profile.doctor_assignments
    ADD COLUMN IF NOT EXISTS analysis_id UUID NULL,
    ADD COLUMN IF NOT EXISTS transferred_from_assignment_id UUID NULL REFERENCES profile.doctor_assignments(assignment_id),
    ADD COLUMN IF NOT EXISTS notes TEXT NULL;

-- Xóa ràng buộc Unique cứng cũ (nếu có) để hỗ trợ phân công theo từng ca chụp và lưu vết chuyển giao
ALTER TABLE profile.doctor_assignments
    DROP CONSTRAINT IF EXISTS uq_clinic_doctor_patient;

-- Tạo unique index linh hoạt cho trạng thái ACTIVE
CREATE UNIQUE INDEX IF NOT EXISTS uq_clinic_doctor_patient_active
    ON profile.doctor_assignments(clinic_id, doctor_account_id, patient_id)
    WHERE status = 'ACTIVE' AND analysis_id IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_clinic_doctor_analysis_active
    ON profile.doctor_assignments(clinic_id, doctor_account_id, analysis_id)
    WHERE status = 'ACTIVE' AND analysis_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_doctor_assignments_analysis
    ON profile.doctor_assignments(analysis_id, status);

-- 3. CẤP QUYỀN TRUY VẬN CHO RUNTIME ROLE
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE profile.patients TO aura_profile_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE profile.doctor_assignments TO aura_profile_app;
