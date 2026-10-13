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
import { handlers } from '../mocks/handlers'
import AnalysisDemoPanel from './AnalysisDemoPanel'

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

const server = setupServer(...handlers)

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
  render(<AnalysisDemoPanel />)

  fireEvent.change(screen.getByLabelText('Kịch bản Analysis'), {
    target: { value: scenario },
  })

  fireEvent.click(
    screen.getByRole('button', { name: 'Chạy demo Analysis' }),
  )
}

describe('Analysis demo UI với API client và MSW', () => {
  it('không hiển thị panel khi mock tắt', () => {
    mockMode.enabled = false

    const { container } = render(<AnalysisDemoPanel />)

    expect(container.innerHTML).toBe('')
  })

  it.each([
    {
      scenario: 'create',
      role: 'status',
      expected: 'Đã tiếp nhận yêu cầu giả lập',
    },
    {
      scenario: 'empty-list',
      role: 'status',
      expected: 'Chưa có hồ sơ phân tích',
    },
    {
      scenario: 'processing',
      role: 'status',
      expected: 'Đang phân tích. Chưa có kết quả nguy cơ.',
    },
    {
      scenario: 'completed',
      role: 'status',
      expected: 'Nguồn: MOCK',
    },
    {
      scenario: 'validation',
      role: 'alert',
      expected: 'HTTP 400 — ERR_VALIDATION',
    },
    {
      scenario: 'insufficient-credit',
      role: 'alert',
      expected: 'HTTP 402 — ERR_INSUFFICIENT_CREDIT',
    },
    {
      scenario: '401',
      role: 'alert',
      expected: 'Đăng nhập lại trước khi tiếp tục.',
    },
    {
      scenario: '403',
      role: 'alert',
      expected: 'Quay về khu vực được phép',
    },
    {
      scenario: 'failed',
      role: 'alert',
      expected: 'Phân tích thất bại.',
    },
    {
      scenario: 'quality-reject',
      role: 'alert',
      expected: 'demo này không đánh giá chất lượng ảnh thật',
    },
  ])('hiển thị đúng scenario $scenario', async ({ scenario, role, expected }) => {
    runScenario(scenario)

    const message = await screen.findByRole(role)

    expect(message.textContent).toContain(expected)

    if (scenario !== 'completed') {
      expect(message.textContent).not.toContain('Nguy cơ minh họa:')
    }

    expect(
      screen.getByRole('button', { name: 'Chạy demo Analysis' }),
    ).toHaveProperty('disabled', false)
  })

  it('timeout hiển thị hướng thử lại và không có kết quả nguy cơ', async () => {
    runScenario('timeout')

    expect(
      screen.getByRole('button', { name: 'Đang chạy demo...' }),
    ).toHaveProperty('disabled', true)

    const message = await screen.findByRole(
      'alert',
      {},
      { timeout: 4500 },
    )

    expect(message.textContent).toContain('hết thời gian chờ')
    expect(message.textContent).toContain('Hãy thử lại')
    expect(message.textContent).toContain('Chưa có kết quả nguy cơ')

    expect(
      screen.getByRole('button', { name: 'Chạy demo Analysis' }),
    ).toHaveProperty('disabled', false)
  }, 6000)
})
