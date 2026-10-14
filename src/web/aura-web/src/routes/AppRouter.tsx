
import { Routes, Route } from 'react-router-dom'
import AppLayout from '../layouts/AppLayout'
import RouteGuard from './RouteGuard'
import type { UserRole } from './roles'
import FeaturePage from '../pages/FeaturePage'
import FormDemoPage from '../pages/FormDemoPage'
import TableDemoPage from '../pages/TableDemoPage'
import ModalDemoPage from '../pages/ModalDemoPage'
import NotFoundPage from '../pages/NotFoundPage'

function DashboardPage() {
  return <h2>Dashboard Page</h2>
}

function PatientsPage() {
  return <h2>Patients Page</h2>
}

function AnalysisPage() {
  return <h2>Analysis Page</h2>
}

function SettingsPage() {
  return <h2>Settings Page</h2>
}

function PatientDashboard() {
  return <h2>Patient Dashboard</h2>
}

function DoctorDashboard() {
  return <h2>Doctor Dashboard</h2>
}

function ClinicDashboard() {
  return <h2>Clinic Dashboard</h2>
}

function AdminDashboard() {
  return <h2>System Admin Dashboard</h2>
}


export default function AppRouter() { const demoUserRole: UserRole = 'SYSTEM_ADMIN'
  return (
      <Routes>
        <Route element={<AppLayout userRole={demoUserRole} />}>
        <Route
  path="admin/modal-demo"
  element={
    <RouteGuard
      userRole={demoUserRole}
      allowedRoles={['SYSTEM_ADMIN']}
    >
      <ModalDemoPage />
    </RouteGuard>
  }
/>

<Route
  path="admin/table-demo"
  element={
    <RouteGuard
      userRole={demoUserRole}
      allowedRoles={['SYSTEM_ADMIN']}
    >
      <TableDemoPage />
    </RouteGuard>
  }
/>

        <Route
  path="admin/form-demo"
  element={
    <RouteGuard
      userRole={demoUserRole}
      allowedRoles={['SYSTEM_ADMIN']}
    >
      <FormDemoPage />
    </RouteGuard>
  }
/>


<Route
  path="patient/dashboard"
  element={
    <RouteGuard
      userRole={demoUserRole}
      allowedRoles={['PATIENT']}
    >
      <PatientDashboard />
    </RouteGuard>
  }
/>


<Route
  path="doctor/dashboard"
  element={
    <RouteGuard
      userRole={demoUserRole}
      allowedRoles={['DOCTOR']}
    >
      <DoctorDashboard />
    </RouteGuard>
  }
/>


<Route
  path="clinic/dashboard"
  element={
    <RouteGuard
      userRole={demoUserRole}
      allowedRoles={['CLINIC']}
    >
      <ClinicDashboard />
    </RouteGuard>
  }
/>


<Route
  path="admin/dashboard"
  element={
    <RouteGuard
      userRole={demoUserRole}
      allowedRoles={['SYSTEM_ADMIN']}
    >
      <AdminDashboard />
    </RouteGuard>
  }

/>

<Route
  path="admin/accounts"
  element={
    <RouteGuard
      userRole={demoUserRole}
      allowedRoles={['SYSTEM_ADMIN']}
    >
      <FeaturePage
        title="Quản lý tài khoản"
        description="Quản lý người dùng và vai trò trong hệ thống AURA."
      />
    </RouteGuard>
  }
/>

<Route
  path="admin/ai-config"
  element={
    <RouteGuard
      userRole={demoUserRole}
      allowedRoles={['SYSTEM_ADMIN']}
    >
      <FeaturePage
        title="Cấu hình AI"
        description="Quản lý cấu hình và thông số hệ thống AI của AURA."
      />
    </RouteGuard>
  }
/>

<Route
  path="admin/audit-logs"
  element={
    <RouteGuard
      userRole={demoUserRole}
      allowedRoles={['SYSTEM_ADMIN']}
    >
      <FeaturePage
        title="Nhật ký hệ thống"
        description="Theo dõi các hoạt động và sự kiện trong hệ thống AURA."
      />
    </RouteGuard>
  }
/>




          <Route path="dashboard" element={<DashboardPage />} />
          <Route
            path="patients"
             element={
            <RouteGuard
              userRole={demoUserRole}
             allowedRoles={['DOCTOR']}>
    <PatientsPage />
    </RouteGuard>
  }
/>
          <Route path="analysis" element={<AnalysisPage />} />
          <Route path="settings" element={<SettingsPage />} />
        </Route>
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
  )
}
