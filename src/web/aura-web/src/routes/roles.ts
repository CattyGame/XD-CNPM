
export type UserRole =
  | 'PATIENT'
  | 'DOCTOR'
  | 'CLINIC'
  | 'SYSTEM_ADMIN'

export const ROLE_LABELS: Record<UserRole, string> = {
  PATIENT: 'Bệnh nhân',
  DOCTOR: 'Bác sĩ',
  CLINIC: 'Cơ sở y tế',
  SYSTEM_ADMIN: 'Quản trị hệ thống',
}
