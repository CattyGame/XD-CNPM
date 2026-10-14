
import { Link } from 'react-router-dom'

export default function ServerErrorPage() {
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
          color: '#B42318',
          fontSize: '64px',
        }}
      >
        500
      </h1>

      <h2>Đã xảy ra lỗi hệ thống</h2>

      <p>
        Hệ thống AURA đang gặp sự cố.
        Vui lòng thử lại sau.
      </p>

      <Link to="/">
        Quay về Dashboard
      </Link>
    </main>
  )
}
