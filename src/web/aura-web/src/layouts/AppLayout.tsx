
import { Outlet } from 'react-router-dom'
import Sidebar from './Sidebar'
import type { UserRole } from '../routes/roles'
import './AppLayout.css'

export default function AppLayout({
  userRole,
}: {
  userRole: UserRole
}) {
  return (
    <div className="app-layout">
      <Sidebar userRole={userRole} />

      <div className="app-content">
        <header>
          <h1>AURA - Retinal Health Screening</h1>
        </header>

        <main>
          <Outlet />
        </main>
      </div>
    </div>
  )
}
