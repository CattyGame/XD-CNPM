
import type { ReactNode } from 'react'
import { Navigate } from 'react-router-dom'
import type { UserRole } from './roles'

type RouteGuardProps = {
  userRole: UserRole | null
  allowedRoles: UserRole[]
  children: ReactNode
}

export default function RouteGuard({
  userRole,
  allowedRoles,
  children,
}: RouteGuardProps) {
  // Chưa đăng nhập
  if (userRole === null) {
    return <Navigate to="/login" replace />
  }

  // Đã đăng nhập nhưng không có quyền
  if (!allowedRoles.includes(userRole)) {
    return <Navigate to="/403" replace />
  }

  // Có quyền thì hiển thị trang
  return <>{children}</>
}
