
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
import './LoginPage.css'

export default function LoginPage() {
  const navigate = useNavigate()

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  const auth = getFirebaseAuth()

  // Tự chuyển về Dashboard nếu đã đăng nhập
  useEffect(() => {
    if (!auth) return

    const unsubscribe = onAuthStateChanged(auth, (user) => {
      if (user) {
        navigate('/', { replace: true })
      }
    })

    return () => unsubscribe()
  }, [auth, navigate])

  // Đăng nhập bằng Email và mật khẩu
  async function loginEmail() {
    setError('')
    setLoading(true)

    try {
      if (!auth) {
        throw new Error('Firebase chưa được cấu hình.')
      }

      await setPersistence(auth, browserLocalPersistence)

      await signInWithEmailAndPassword(
        auth,
        email.trim(),
        password
      )

      navigate('/', { replace: true })
    } catch {
      setError(
        'Đăng nhập thất bại. Vui lòng kiểm tra email và mật khẩu.'
      )
    } finally {
      setLoading(false)
    }
  }

  // Đăng nhập bằng Google
  async function loginGoogle() {
    setError('')
    setLoading(true)

    try {
      if (!auth) {
        throw new Error('Firebase chưa được cấu hình.')
      }

      await setPersistence(auth, browserLocalPersistence)

      await signInWithPopup(
        auth,
        new GoogleAuthProvider()
      )

      navigate('/', { replace: true })
    } catch {
      setError(
        'Không thể đăng nhập Google. Vui lòng thử lại.'
      )
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="aura-login">
      <div className="aura-login-container">

        {/* BÊN TRÁI: GIỚI THIỆU AURA */}
        <section className="aura-login-brand">
          <div className="aura-login-logo">
            <span
              className="aura-login-logo-icon"
              aria-hidden="true"
            >
              ◉
            </span>
            <span>AURA</span>
          </div>

          <div className="aura-login-brand-content">
            <h1>
              Chăm sóc sức khỏe
              <br />
              bằng công nghệ AI
            </h1>

            <p>
              Hệ thống sàng lọc sức khỏe mạch máu
              võng mạc, hỗ trợ phân tích hình ảnh
              và cung cấp thông tin phục vụ
              quá trình đánh giá sức khỏe.
            </p>
          </div>

          <div className="aura-login-brand-footer">
            © 2026 AURA · Retinal Health Screening System
          </div>
        </section>

        {/* BÊN PHẢI: FORM ĐĂNG NHẬP */}
        <section className="aura-login-form-section">
          <div className="aura-login-heading">
            <h2>Chào mừng trở lại!</h2>
            <p>
              Đăng nhập để tiếp tục sử dụng
              hệ thống AURA.
            </p>
          </div>

          <form
            onSubmit={(event) => {
              event.preventDefault()
              void loginEmail()
            }}
          >
            {/* EMAIL */}
            <div className="aura-login-field">
              <label htmlFor="email">
                Địa chỉ Email
              </label>

              <div className="aura-login-input-wrapper">
                <input
                  id="email"
                  type="email"
                  autoComplete="email"
                  placeholder="Nhập địa chỉ email"
                  required
                  value={email}
                  disabled={loading}
                  onChange={(event) => {
                    setEmail(event.target.value)
                  }}
                />
              </div>
            </div>

            {/* MẬT KHẨU */}
            <div className="aura-login-field">
              <label htmlFor="password">
                Mật khẩu
              </label>

              <div className="aura-login-input-wrapper">
                <input
                  id="password"
                  className="has-toggle"
                  type={showPassword ? 'text' : 'password'}
                  autoComplete="current-password"
                  placeholder="Nhập mật khẩu"
                  required
                  value={password}
                  disabled={loading}
                  onChange={(event) => {
                    setPassword(event.target.value)
                  }}
                />

                <button
                  type="button"
                  className="aura-login-password-toggle"
                  aria-label={
                    showPassword
                      ? 'Ẩn mật khẩu'
                      : 'Hiện mật khẩu'
                  }
                  aria-pressed={showPassword}
                  onClick={() => {
                    setShowPassword(!showPassword)
                  }}
                >
                  {showPassword ? 'Ẩn' : 'Hiện'}
                </button>
              </div>
            </div>

            {/* THÔNG BÁO LỖI */}
            {error && (
              <p className="aura-login-error" role="alert">
                {error}
              </p>
            )}

            {/* NÚT ĐĂNG NHẬP */}
            <button
              type="submit"
              className="aura-login-submit"
              disabled={loading}
            >
              {loading
                ? 'Đang đăng nhập...'
                : 'Đăng nhập'}
            </button>
          </form>

          {/* ĐƯỜNG PHÂN CÁCH */}
          <div className="aura-login-divider">
            hoặc tiếp tục với
          </div>

          {/* ĐĂNG NHẬP GOOGLE */}
          <button
            type="button"
            className="aura-login-google"
            disabled={loading}
            onClick={() => void loginGoogle()}
          >
            <span
              className="aura-login-google-icon"
              aria-hidden="true"
            >
              G
            </span>
            Tiếp tục với Google
          </button>

          <p className="aura-login-note">
            AURA hỗ trợ sàng lọc và cung cấp
            thông tin tham khảo, không thay thế
            chẩn đoán của bác sĩ.
          </p>
        </section>
      </div>
    </main>
  )
}
