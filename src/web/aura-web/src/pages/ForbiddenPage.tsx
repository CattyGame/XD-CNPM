
import { Link } from 'react-router-dom'

export default function ForbiddenPage() {
  return (
    <main style={{
      minHeight: '100vh',
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      justifyContent: 'center',
      textAlign: 'center',
      padding: '24px',
      backgroundColor: '#F8FAFC',
    }}>
      <h1 style={{ color: '#B42318', fontSize: '64px' }}>
        403
      </h1>

      <h2>Không có quyền truy cập</h2>

      <p>
        Tài khoản của bạn không có quyền truy cập trang này.
      </p>

      <Link to="/">
        Quay về Dashboard
      </Link>
    </main>
  )
}
