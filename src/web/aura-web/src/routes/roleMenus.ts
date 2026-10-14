
import type { UserRole } from './roles'

export type MenuItem = {
  label: string
  path: string
}

export const roleMenus: Record<UserRole, MenuItem[]> = {
  PATIENT: [
    {
      label: 'Dashboard',
      path: '/router-demo/patient/dashboard',
    },
  ],

  DOCTOR: [
    {
      label: 'Dashboard',
      path: '/router-demo/doctor/dashboard',
    },
  ],

  CLINIC: [
    {
      label: 'Dashboard',
      path: '/router-demo/clinic/dashboard',
    },
  ],

  SYSTEM_ADMIN: [
    {
      label: 'Dashboard',
      path: '/router-demo/admin/dashboard',
      
    },
    {
      label: 'Quản lý tài khoản',
      path: '/router-demo/admin/accounts',
    },
    
    {
      label: 'Cấu hình AI',
      path: '/router-demo/admin/ai-config',
    },
    
    {
      label: 'Nhật ký hệ thống',
      path: '/router-demo/admin/audit-logs',
    },


  ],
}
