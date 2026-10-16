import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  GoogleAuthProvider,
  signInWithEmailAndPassword,
  signInWithPopup,
  browserLocalPersistence,
  setPersistence,
  onAuthStateChanged,
} from 'firebase/auth'
import { getFirebaseAuth } from '../firebase/config'

export default function LoginPage() {
  const navigate = useNavigate()

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  const auth = getFirebaseAuth()

  useEffect(() => {
  if (!auth) return

  const unsubscribe = onAuthStateChanged(auth, (user) => {
    if (user) {
      navigate('/', { replace: true })
    }
  })

  return () => unsubscribe()
}, [auth, navigate])

  async function loginEmail() {
    setError('')
    setLoading(true)

    try {
      if (!auth) {
        throw new Error('Firebase chưa được cấu hình.')
      }

      await setPersistence(auth, browserLocalPersistence)
      await signInWithEmailAndPassword(auth, email.trim(), password)

      navigate('/')
    } catch {
      setError('Đăng nhập thất bại. Vui lòng kiểm tra email và mật khẩu.')
    } finally {
      setLoading(false)
    }
  }

  async function loginGoogle() {
    setError('')
    setLoading(true)

    try {
      if (!auth) {
        throw new Error('Firebase chưa được cấu hình.')
      }

      await setPersistence(auth, browserLocalPersistence)
      await signInWithPopup(auth, new GoogleAuthProvider())

      navigate('/')
    } catch {
      setError('Không thể đăng nhập Google. Vui lòng thử lại.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <main style={{ maxWidth: 400, margin: '80px auto', padding: 24 }}>
      <h1>AURA — Đăng nhập</h1>
      <p>Hệ thống sàng lọc sức khỏe mạch máu võng mạc</p>

      <form
        onSubmit={(event) => {
          event.preventDefault()
          void loginEmail()
        }}
      >
        <label htmlFor="email">Email</label>
        <input
          id="email"
          type="email"
          required
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          style={{ display: 'block', width: '100%', marginBottom: 16 }}
        />

        <label htmlFor="password">Mật khẩu</label>
        <input
          id="password"
          type="password"
          required
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          style={{ display: 'block', width: '100%', marginBottom: 16 }}
        />

        {error && <p role="alert" style={{ color: 'red' }}>{error}</p>}

        <button type="submit" disabled={loading}>
          {loading ? 'Đang xử lý...' : 'Đăng nhập bằng Email'}
        </button>
      </form>

      <hr />

      <button
        type="button"
        onClick={() => void loginGoogle()}
        disabled={loading}
      >
        Tiếp tục với Google
      </button>
    </main>
  )
}