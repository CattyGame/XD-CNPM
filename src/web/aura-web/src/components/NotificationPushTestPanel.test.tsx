// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { MessagePayload } from 'firebase/messaging'
import NotificationPushTestPanel from './NotificationPushTestPanel'

const m = vi.hoisted(() => ({ mock: false, listen: vi.fn(), token: vi.fn(), register: vi.fn(), send: vi.fn() }))
vi.mock('../api/client', () => ({ get isMockEnabled() { return m.mock } }))
vi.mock('../firebase/messaging', () => ({ listenForegroundMessages: m.listen, obtainFcmToken: m.token }))
vi.mock('../api/notificationDevices', () => ({
  registerNotificationDevice: m.register,
  NotificationDeviceError: class extends Error {
    readonly status: number
    readonly code: string

    constructor(status: number, code: string) {
      super(code)
      this.status = status
      this.code = code
    }
  },
}))
vi.mock('../api/notificationTestPush', () => ({ sendNotificationTestPush: m.send }))

let receive: (payload: MessagePayload) => void
const stop = vi.fn()
const busy = vi.fn()
const id = '1234567890abcdef1234567890abcdef'
beforeEach(() => {
  vi.resetAllMocks()
  vi.stubGlobal('isSecureContext', true)
  vi.stubGlobal('Notification', { permission: 'granted' })
  m.mock = false
  m.listen.mockImplementation(async (callback) => { receive = callback; return stop })
  m.token.mockResolvedValue({ status: 'ready', token: 'private-fcm-token' })
  m.register.mockResolvedValue(undefined)
  m.send.mockResolvedValue({ accepted: true, testId: id })
})
afterEach(() => { cleanup(); vi.unstubAllGlobals() })
function panel(userId: string | null = 'uid-a') {
  return render(<NotificationPushTestPanel userId={userId} parentBusy={false} onBusyChange={busy} />)
}
async function ready() { await screen.findByText('Sẵn sàng nhận thông báo foreground.') }

describe('Push thật', () => {
  it('chưa đăng nhập thì không mở listener hoặc gửi', () => {
    panel(null)
    expect(m.listen).not.toHaveBeenCalled()
    expect(screen.getByRole('button', { name: 'Gửi push kiểm thử' })).toHaveProperty('disabled', true)
  })
  it('mock không khởi tạo FCM thật', () => {
    m.mock = true
    panel()
    expect(m.listen).not.toHaveBeenCalled()
    expect(m.token).not.toHaveBeenCalled()
  })
  it('đồng bộ token trước khi gửi và không hiển thị token', async () => {
    panel()
    await ready()
    fireEvent.click(screen.getByRole('button', { name: 'Gửi push kiểm thử' }))
    await screen.findByText(/FCM đã chấp nhận gửi/)
    expect(m.register).toHaveBeenCalledWith('private-fcm-token')
    expect(m.register.mock.invocationCallOrder[0]).toBeLessThan(m.send.mock.invocationCallOrder[0])
    expect(m.send).toHaveBeenCalledWith('uid-a', expect.any(AbortSignal))
    expect(document.body.textContent).not.toContain('private-fcm-token')
    expect(busy).toHaveBeenLastCalledWith(false)
  })
  it('hiển thị mã nhận foreground nhưng không hiển thị nội dung payload', async () => {
    panel()
    await ready()
    act(() => receive({ from: 'test-sender', collapseKey: 'aura-test', messageId: 'test-message', data: { kind: 'aura-test', testId: id }, notification: { body: 'private-health-data' } }))
    expect(screen.getByText(/Đã nhận thông báo foreground/).textContent).toContain(id)
    expect(document.body.textContent).not.toContain('private-health-data')
  })
  it('quyền denied không lấy token hoặc gọi API', async () => {
    vi.stubGlobal('Notification', { permission: 'denied' })
    panel()
    await ready()
    fireEvent.click(screen.getByRole('button', { name: 'Gửi push kiểm thử' }))
    expect(screen.getByRole('alert').textContent).toContain('cấp quyền')
    expect(m.token).not.toHaveBeenCalled()
  })
  it('lỗi gửi không hiển thị chi tiết nội bộ và mở lại nút', async () => {
    m.send.mockRejectedValue(new Error('private-internal-detail'))
    panel()
    await ready()
    fireEvent.click(screen.getByRole('button', { name: 'Gửi push kiểm thử' }))
    await screen.findByRole('alert')
    expect(document.body.textContent).not.toContain('private-internal-detail')
    expect(screen.getByRole('button', { name: 'Gửi push kiểm thử' })).toHaveProperty('disabled', false)
  })
  it('unmount dừng listener', async () => {
    const view = panel()
    await ready()
    view.unmount()
    expect(stop).toHaveBeenCalledTimes(1)
  })
  it('listener khởi tạo muộn sau unmount được hủy ngay', async () => {
    let resolve!: (stop: () => void) => void
    m.listen.mockImplementation(() => new Promise<() => void>((r) => { resolve = r }))
    const view = panel()
    view.unmount()
    await act(async () => resolve(stop))
    await waitFor(() => expect(stop).toHaveBeenCalledTimes(1))
  })
  it('đăng xuất khi đang chờ token không đăng ký hoặc gửi tiếp', async () => {
    let resolve!: (token: { status: string; token: string }) => void
    m.token.mockImplementation(() => new Promise((r) => { resolve = r }))
    const view = panel()
    await ready()
    fireEvent.click(screen.getByRole('button', { name: 'Gửi push kiểm thử' }))
    view.rerender(<NotificationPushTestPanel userId={null} parentBusy={false} onBusyChange={busy} />)
    await act(async () => resolve({ status: 'ready', token: 'private-fcm-token' }))
    expect(m.register).not.toHaveBeenCalled()
    expect(m.send).not.toHaveBeenCalled()
  })
})
