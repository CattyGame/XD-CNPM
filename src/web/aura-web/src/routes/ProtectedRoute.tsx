import { useEffect, useState, type ReactNode } from 'react'
import { Navigate } from 'react-router-dom'
import { onAuthStateChanged, type User } from 'firebase/auth'
import { getFirebaseAuth } from '../firebase/config'

type ProtectedRouteProps = {
  children: ReactNode
}

export default function ProtectedRoute({ children }: ProtectedRouteProps) {
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const auth = getFirebaseAuth()

    if (!auth) {
      setLoading(false)
      return
    }

    const unsubscribe = onAuthStateChanged(
      auth,
      (firebaseUser) => {
        setUser(firebaseUser)
        setLoading(false)
      },
      (error) => {
        console.error('Lỗi kiểm tra phiên đăng nhập:', error)
        setLoading(false)
      },
    )

    return () => unsubscribe()
  }, [])

  if (loading) {
    return <p>Đang kiểm tra phiên đăng nhập...</p>
  }

  if (!user) {
    return <Navigate to="/login" replace />
  }

  return <>{children}</>
}