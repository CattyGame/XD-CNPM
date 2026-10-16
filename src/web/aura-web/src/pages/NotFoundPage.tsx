
import { Link } from 'react-router-dom'

export default function NotFoundPage() {
  return (
    <main
      style={{
        minHeight: '100vh',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        textAlign: 'center',
        padding: '24px',
        backgroundColor: '#F8FAFC',
      }}
    >
      <h1
        style={{
          color: '#155EEF',
          fontSize: '64px',
        }}
      >
        404
      </h1>

      <h2>Không tìm thấy trang</h2>

      <p>
        Đường dẫn bạn truy cập không tồn tại trong hệ thống AURA.
      </p>

      <Link to="/">
        Quay về Dashboard
      </Link>
    </main>
  )
}
