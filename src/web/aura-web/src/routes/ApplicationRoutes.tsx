import type { ReactNode } from 'react'
import { Link, Route, Routes } from 'react-router-dom'
import App from '../App'
import LoginPage from '../pages/LoginPage'
import UploadPage from '../pages/UploadPage'
import SessionHomePage from '../pages/SessionHomePage'
import ForbiddenPage from '../pages/ForbiddenPage'
import NotFoundPage from '../pages/NotFoundPage'
import ServerErrorPage from '../pages/ServerErrorPage'
import ProtectedRoute from './ProtectedRoute'
import AppRouter from './AppRouter'

function DemoScope({ children }: { children: ReactNode }) {
  return <>
    <aside role="note" aria-label="Giao diện minh họa" style={{ position: 'relative', zIndex: 1000, padding: 16, background: '#fff3cd', color: '#5d4800' }}>
      <strong>Giao diện minh họa.</strong> Vai trò và dữ liệu trong khu vực này là giả lập.{' '}
      <Link to="/">Về trang tài khoản thật</Link>
    </aside>
    {children}
  </>
}

export default function ApplicationRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/403" element={<ForbiddenPage />} />
      <Route path="/500" element={<ServerErrorPage />} />
      <Route path="/router-demo/*" element={<DemoScope><AppRouter /></DemoScope>} />
      <Route path="/demo/*" element={<ProtectedRoute><DemoScope><App /></DemoScope></ProtectedRoute>} />
      <Route path="/upload" element={<ProtectedRoute><UploadPage /></ProtectedRoute>} />
      <Route path="/" element={<ProtectedRoute><SessionHomePage /></ProtectedRoute>} />
      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  )
}
