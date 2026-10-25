import type { ReactNode } from 'react'
import { Navigate } from 'react-router-dom'
import { useBackendSession } from '../auth/backendSession'
import BackendSessionNotice from '../auth/BackendSessionNotice'

export default function ProtectedRoute({ children }: { children: ReactNode }) {
  const { state } = useBackendSession()
  if (state.status === 'anonymous') return <Navigate to="/login" replace />
  if (state.status !== 'ready') return <BackendSessionNotice />
  return <>{children}</>
}
