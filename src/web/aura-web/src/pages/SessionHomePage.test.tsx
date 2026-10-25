import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { BackendSessionContext } from '../auth/backendSession'
import type { BackendSessionState } from '../auth/backendSessionStore'
import { BackendContextError, type BackendContext } from '../api/backendContext'
import SessionHomePage from './SessionHomePage'

const context: BackendContext = {
  accountId: '11111111-1111-4111-8111-111111111111',
  patientId: '22222222-2222-4222-8222-222222222222',
  personalTenantId: '33333333-3333-4333-8333-333333333333',
  firebaseUid: 'synthetic-test-user', roles: ['PATIENT'],
  displayName: 'Hồ sơ kiểm thử', profileVersion: 1,
}

afterEach(() => { cleanup(); vi.unstubAllGlobals() })

function show(state: BackendSessionState = { status: 'ready', context }, logout = vi.fn(async () => {})) {
  return render(
    <BackendSessionContext.Provider value={{ state, logout, retry: vi.fn() }}>
      <MemoryRouter>
        <Routes>
          <Route path="/" element={<SessionHomePage />} />
          <Route path="/upload" element={<p>Trang upload thực</p>} />
        </Routes>
      </MemoryRouter>
    </BackendSessionContext.Provider>,
  )
}

describe('Trang chủ tài khoản đã xác nhận', () => {
  it('hiển thị tên/vai trò thật, không có quản trị viên hoặc hồ sơ demo cố định', () => {
    show()
    expect(screen.getByRole('heading', { name: 'Xin chào, Hồ sơ kiểm thử' })).toBeTruthy()
    expect(screen.getByText('Bệnh nhân')).toBeTruthy()
    expect(screen.queryByText('System Admin')).toBeNull()
    expect(screen.queryByText('Quản trị viên')).toBeNull()
    expect(screen.queryByText('Nguyễn Minh Anh')).toBeNull()
    expect(screen.queryByText('Tổng hồ sơ')).toBeNull()
    expect(screen.queryByText(/DEMO/)).toBeNull()
  })

  it.each([
    ['PATIENT', 'Bệnh nhân'], ['DOCTOR', 'Bác sĩ'],
    ['CLINIC', 'Cơ sở y tế'], ['SYSTEM_ADMIN', 'Quản trị hệ thống'],
  ] as const)('dùng đúng nhãn của role %s từ context', (role, label) => {
    show({ status: 'ready', context: { ...context, roles: [role] } })
    expect(screen.getByText(label)).toBeTruthy()
  })

  it('hiển thị tất cả role được xác nhận và giữ luồng cá nhân cho Patient/Doctor', () => {
    show({ status: 'ready', context: { ...context, roles: ['DOCTOR', 'PATIENT'] } })
    expect(screen.getByText('Bác sĩ')).toBeTruthy()
    expect(screen.getByText('Bệnh nhân')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Mở trang tải ảnh' }).getAttribute('href')).toBe('/upload')
  })

  it.each([[['DOCTOR']], [['CLINIC']], [['SYSTEM_ADMIN']], [['SYSTEM_ADMIN', 'PATIENT']]] as [BackendContext['roles']][])(
    'không quảng bá upload cá nhân ngoài policy: %j', (roles) => {
      show({ status: 'ready', context: { ...context, roles } })
      expect(screen.queryByRole('link', { name: 'Mở trang tải ảnh' })).toBeNull()
      expect(screen.queryByRole('link', { name: 'Tải ảnh võng mạc' })).toBeNull()
    },
  )

  it('đưa Patient đến /upload, không mở modal upload demo', () => {
    show()
    fireEvent.click(screen.getByRole('link', { name: 'Mở trang tải ảnh' }))
    expect(screen.getByText('Trang upload thực')).toBeTruthy()
    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('gọi logout của session chung và chờ session đổi thay vì tự điều hướng', () => {
    const logout = vi.fn(async () => {})
    show({ status: 'ready', context }, logout)
    fireEvent.click(screen.getByRole('button', { name: 'Đăng xuất' }))
    expect(logout).toHaveBeenCalledTimes(1)
    expect(screen.getByRole('heading', { name: 'Trang chủ AURA' })).toBeTruthy()
  })

  it('không gọi HTTP khi hiển thị trang chủ từ context', () => {
    const fetch = vi.fn()
    vi.stubGlobal('fetch', fetch)
    show()
    expect(fetch).not.toHaveBeenCalled()
    expect(screen.queryByText(context.firebaseUid)).toBeNull()
    expect(screen.queryByText(context.accountId)).toBeNull()
    expect(screen.queryByText(context.patientId)).toBeNull()
  })

  it('không hiển thị danh tính cũ khi session đang loading', () => {
    show({ status: 'loading', firebaseUid: context.firebaseUid })
    expect(screen.getByRole('status').textContent).toContain('Đang xác nhận')
    expect(screen.queryByText(/Hồ sơ kiểm thử/)).toBeNull()
  })

  it('hiển thị notice chuẩn khi session lỗi, không hiển thị role hoặc link upload', () => {
    show({ status: 'error', firebaseUid: context.firebaseUid,
      error: new BackendContextError(0, 'ERR_SIGN_OUT_FAILED') })
    expect(screen.getByRole('alert').textContent).toBe('Không thể đăng xuất. Vui lòng thử lại.')
    expect(screen.queryByText('Bệnh nhân')).toBeNull()
    expect(screen.queryByRole('link', { name: 'Mở trang tải ảnh' })).toBeNull()
  })

  it('escape tên hồ sơ như văn bản', () => {
    const name = '<img src=x onerror=alert(1)>'
    const { container } = show({ status: 'ready', context: { ...context, displayName: name } })
    expect(screen.getByRole('heading', { name: 'Xin chào, ' + name })).toBeTruthy()
    expect(container.querySelector('img')).toBeNull()
  })
})
