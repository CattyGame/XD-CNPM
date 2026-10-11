// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import {
  afterAll,
  afterEach,
  beforeAll,
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from 'vitest'
import { setupServer } from 'msw/node'
import { fcmHandlers } from '../mocks/fcmHandlers'
import FcmDemoPanel from './FcmDemoPanel'

const mockMode = vi.hoisted(() => ({ enabled: true }))

vi.mock('../api/client', async (importOriginal) => {
  vi.stubEnv('VITE_ENABLE_MOCK', 'true')
  vi.stubEnv('VITE_API_BASE_URL', '')

  const actual = await importOriginal<typeof import('../api/client')>()

  return {
    ...actual,
    get isMockEnabled() {
      return mockMode.enabled
    },
  }
})

const server = setupServer(...fcmHandlers)

beforeAll(() => {
  server.listen({ onUnhandledRequest: 'error' })
})

beforeEach(() => {
  mockMode.enabled = true
})

afterEach(() => {
  cleanup()
  server.resetHandlers()
})

afterAll(() => {
  server.close()
  vi.unstubAllEnvs()
})

function runScenario(scenario: string) {
  render(<FcmDemoPanel />)

  fireEvent.change(screen.getByLabelText('Kịch bản FCM demo'), {
    target: { value: scenario },
  })

  fireEvent.click(
    screen.getByRole('button', { name: 'Chạy demo FCM' }),
  )
}

describe('FCM demo UI với API client và MSW', () => {
  it('không hiển thị panel khi mock tắt', () => {
    mockMode.enabled = false

    const { container } = render(<FcmDemoPanel />)

    expect(container.innerHTML).toBe('')
  })

  it('thành công vẫn ghi rõ chưa gửi push thật', async () => {
    runScenario('success')

    const message = await screen.findByRole('status')

    expect(message.textContent).toContain('200 —')
    expect(message.textContent).toContain(
      'Đăng ký thông báo demo thành công',
    )
    expect(message.textContent).toContain('Chưa gửi push thật')

    expect(
      screen.getByRole('button', { name: 'Chạy demo FCM' }),
    ).toHaveProperty('disabled', false)
  })

  it.each([
    {
      scenario: 'unauthorized',
      status: 401,
      code: 'DEMO_FCM_UNAUTHORIZED',
      recovery: 'Đăng nhập lại trước khi tiếp tục.',
    },
    {
      scenario: 'forbidden',
      status: 403,
      code: 'DEMO_FCM_FORBIDDEN',
      recovery: 'Liên hệ quản trị viên để kiểm tra quyền truy cập.',
    },
    {
      scenario: 'server-error',
      status: 500,
      code: 'DEMO_FCM_SERVER_ERROR',
      recovery: 'Hãy thử lại sau.',
    },
  ])(
    'scenario $scenario hiển thị lỗi và hướng xử lý',
    async ({ scenario, status, code, recovery }) => {
      runScenario(scenario)

      const message = await screen.findByRole('alert')

      expect(message.textContent).toContain(`HTTP ${status}`)
      expect(message.textContent).toContain(code)
      expect(message.textContent).toContain(recovery)
      expect(screen.queryByRole('status')).toBeNull()

      expect(
        screen.getByRole('button', { name: 'Chạy demo FCM' }),
      ).toHaveProperty('disabled', false)
    },
  )

  it('timeout khóa điều khiển khi chờ và cho phép thử lại', async () => {
    runScenario('timeout')

    expect(
      screen.getByRole('button', { name: 'Đang kiểm thử...' }),
    ).toHaveProperty('disabled', true)

    expect(
      screen.getByLabelText('Kịch bản FCM demo'),
    ).toHaveProperty('disabled', true)

    const message = await screen.findByRole(
      'alert',
      {},
      { timeout: 4500 },
    )

    expect(message.textContent).toContain('hết thời gian chờ')
    expect(message.textContent).toContain('Hãy thử lại')
    expect(message.textContent).toContain(
      'Chưa xác nhận đăng ký thông báo',
    )
    expect(screen.queryByRole('status')).toBeNull()

    expect(
      screen.getByRole('button', { name: 'Chạy demo FCM' }),
    ).toHaveProperty('disabled', false)

    expect(
      screen.getByLabelText('Kịch bản FCM demo'),
    ).toHaveProperty('disabled', false)
  }, 6000)

  it('đổi scenario xóa kết quả cũ và có thể chạy lại', async () => {
    runScenario('success')

    await screen.findByRole('status')

    fireEvent.change(screen.getByLabelText('Kịch bản FCM demo'), {
      target: { value: 'unauthorized' },
    })

    expect(screen.queryByRole('status')).toBeNull()
    expect(screen.queryByRole('alert')).toBeNull()

    fireEvent.click(
      screen.getByRole('button', { name: 'Chạy demo FCM' }),
    )

    const message = await screen.findByRole('alert')

    expect(message.textContent).toContain('HTTP 401')
    expect(message.textContent).toContain(
      'Đăng nhập lại trước khi tiếp tục.',
    )
  })
})