// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import {
  afterEach,
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest'
import NotificationSettings from './NotificationSettings'

const mocks = vi.hoisted(() => ({
  getPermission: vi.fn(),
  requestPermission: vi.fn(),
  registerWorker: vi.fn(),
}))

vi.mock('../firebase/notificationPermission', () => ({
  getNotificationPermission: mocks.getPermission,
  requestNotificationPermission: mocks.requestPermission,
}))

vi.mock('../firebase/serviceWorker', () => ({
  registerFirebaseServiceWorker: mocks.registerWorker,
}))

beforeEach(() => {
  vi.resetAllMocks()
  vi.stubGlobal('isSecureContext', true)

  mocks.getPermission.mockReturnValue('default')
  mocks.requestPermission.mockResolvedValue('granted')
  mocks.registerWorker.mockResolvedValue({})
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

function enableNotifications() {
  fireEvent.click(
    screen.getByRole('button', { name: 'Bật thông báo' }),
  )
}

describe('Cài đặt quyền thông báo AURA', () => {
  it('không tự xin quyền hoặc đăng ký worker khi mở trang', () => {
    render(<NotificationSettings />)

    expect(screen.getByRole('status').textContent).toContain(
      'Chỉ yêu cầu quyền thông báo khi bạn nhấn',
    )
    expect(mocks.requestPermission).not.toHaveBeenCalled()
    expect(mocks.registerWorker).not.toHaveBeenCalled()
  })

  it('quyền bị chặn hiển thị hướng xử lý và khóa nút', () => {
    mocks.getPermission.mockReturnValue('denied')

    render(<NotificationSettings />)

    expect(screen.getByRole('status').textContent).toContain(
      'Mở cài đặt quyền của trang',
    )
    expect(
      screen.getByRole('button', { name: 'Bật thông báo' }),
    ).toHaveProperty('disabled', true)

    enableNotifications()

    expect(mocks.requestPermission).not.toHaveBeenCalled()
    expect(mocks.registerWorker).not.toHaveBeenCalled()
  })

  it('trình duyệt không hỗ trợ thì khóa nút và giải thích', () => {
    mocks.getPermission.mockReturnValue('unsupported')

    render(<NotificationSettings />)

    expect(screen.getByRole('status').textContent).toContain(
      'Trình duyệt không hỗ trợ thông báo',
    )
    expect(
      screen.getByRole('button', { name: 'Bật thông báo' }),
    ).toHaveProperty('disabled', true)
    expect(mocks.requestPermission).not.toHaveBeenCalled()
  })

  it('môi trường không an toàn yêu cầu HTTPS hoặc localhost', () => {
    vi.stubGlobal('isSecureContext', false)

    render(<NotificationSettings />)

    expect(screen.getByRole('status').textContent).toContain(
      'Thông báo cần HTTPS hoặc localhost',
    )
    expect(
      screen.getByRole('button', { name: 'Bật thông báo' }),
    ).toHaveProperty('disabled', true)

    enableNotifications()

    expect(mocks.requestPermission).not.toHaveBeenCalled()
    expect(mocks.registerWorker).not.toHaveBeenCalled()
  })

  it('bấm bật xin quyền và đăng ký scaffold khi được cấp quyền', async () => {
    render(<NotificationSettings />)

    enableNotifications()

    await screen.findByText(
      'Đã cấp quyền và đăng ký service worker scaffold. Chưa xác nhận FCM token hoặc push thật.',
    )

    expect(mocks.requestPermission).toHaveBeenCalledTimes(1)
    expect(mocks.registerWorker).toHaveBeenCalledTimes(1)
    expect(screen.getByRole('status').textContent).toContain(
      'Chưa xác nhận FCM token hoặc push thật',
    )
    expect(
      screen.getByRole('button', { name: 'Bật thông báo' }),
    ).toHaveProperty('disabled', false)
  })

  it('người dùng từ chối quyền thì không đăng ký worker', async () => {
    mocks.requestPermission.mockResolvedValue('denied')

    render(<NotificationSettings />)

    enableNotifications()

    await screen.findByText(/Thông báo đã bị chặn/)

    expect(mocks.registerWorker).not.toHaveBeenCalled()
    expect(
      screen.getByRole('button', { name: 'Bật thông báo' }),
    ).toHaveProperty('disabled', true)
  })

  it('chưa quyết định cấp quyền thì có thể thử lại', async () => {
    mocks.requestPermission.mockResolvedValue('default')

    render(<NotificationSettings />)

    enableNotifications()

    await screen.findByRole('button', { name: 'Bật thông báo' })

    expect(mocks.requestPermission).toHaveBeenCalledTimes(1)
    expect(mocks.registerWorker).not.toHaveBeenCalled()
    expect(screen.getByRole('status').textContent).toContain(
      'Chỉ yêu cầu quyền thông báo khi bạn nhấn',
    )
    expect(
      screen.getByRole('button', { name: 'Bật thông báo' }),
    ).toHaveProperty('disabled', false)
  })

  it('đăng ký worker thất bại hiển thị lỗi và cho phép thử lại', async () => {
    mocks.registerWorker.mockResolvedValue(null)

    render(<NotificationSettings />)

    enableNotifications()

    const message = await screen.findByRole('alert')

    expect(message.textContent).toContain(
      'chưa đăng ký được service worker',
    )
    expect(message.textContent).toContain('thử lại')
    expect(
      screen.getByRole('button', { name: 'Bật thông báo' }),
    ).toHaveProperty('disabled', false)
  })

  it('xin quyền gặp lỗi hiển thị hướng thử lại', async () => {
    mocks.requestPermission.mockRejectedValue(
      new Error('Permission request failed'),
    )

    render(<NotificationSettings />)

    enableNotifications()

    const message = await screen.findByRole('alert')

    expect(message.textContent).toContain(
      'Không thể bật thông báo. Vui lòng thử lại.',
    )
    expect(mocks.registerWorker).not.toHaveBeenCalled()
    expect(
      screen.getByRole('button', { name: 'Bật thông báo' }),
    ).toHaveProperty('disabled', false)
  })

  it('quay lại cửa sổ cập nhật quyền mà không tự đăng ký worker', () => {
    mocks.getPermission.mockReturnValue('denied')

    render(<NotificationSettings />)

    expect(
      screen.getByRole('button', { name: 'Bật thông báo' }),
    ).toHaveProperty('disabled', true)

    mocks.getPermission.mockReturnValue('granted')
    fireEvent(window, new Event('focus'))

    expect(screen.getByRole('status').textContent).toContain(
      'Trình duyệt đã cấp quyền thông báo',
    )
    expect(
      screen.getByRole('button', { name: 'Bật thông báo' }),
    ).toHaveProperty('disabled', false)
    expect(mocks.requestPermission).not.toHaveBeenCalled()
    expect(mocks.registerWorker).not.toHaveBeenCalled()
  })
})