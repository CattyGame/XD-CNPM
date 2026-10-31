import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { BackendContext } from '../api/backendContext'
import { BackendSessionContext } from '../auth/backendSession'
import { BatchProgressError, type BatchProgress } from '../api/batchProgress'

const mocks = vi.hoisted(() => ({ get: vi.fn() }))
vi.mock('../api/batchProgress', async (actual) => ({
  ...await actual<typeof import('../api/batchProgress')>(), getBatchProgress: mocks.get,
}))
import BatchProgressPage from './BatchProgressPage'

const batchId = '40000000-0000-4000-8000-000000000004'
const context: BackendContext = {
  firebaseUid: 'user-a', accountId: '10000000-0000-4000-8000-000000000001',
  patientId: '20000000-0000-4000-8000-000000000002', personalTenantId: '30000000-0000-4000-8000-000000000003',
  roles: ['PATIENT'], displayName: 'Patient A', profileVersion: 1,
}
const terminal: BatchProgress = {
  batchId, batchStatus: 'COMPLETED', createdAt: '2026-10-27T03:00:00Z',
  items: [{ analysisId: '50000000-0000-4000-8000-000000000005', fileName: 'fundus.png',
    processingStatus: 'COMPLETED', reviewStatus: 'PENDING', riskLevel: 'MODERATE' }],
  summary: { total: 1, active: 0, succeeded: 1, failed: 0, duplicate: 0 },
}
function show(path = `/batches/${batchId}`) {
  return render(<BackendSessionContext.Provider value={{ state: { status: 'ready', context }, retry: vi.fn(), logout: vi.fn() }}>
    <MemoryRouter initialEntries={[path]}><Routes>
      <Route path="/batches" element={<BatchProgressPage />} />
      <Route path="/batches/:batchId" element={<BatchProgressPage />} />
    </Routes></MemoryRouter>
  </BackendSessionContext.Provider>)
}
beforeEach(() => {
  vi.resetAllMocks()
  mocks.get.mockResolvedValue({ kind: 'batch', data: terminal })
})
afterEach(() => { cleanup(); vi.useRealTimers() })

describe('SCRUM-332 batch progress component', () => {
  it('opens a camera or web batch by ID and renders the authorized server result', async () => {
    show('/batches')
    fireEvent.change(screen.getByLabelText('Batch ID'), { target: { value: batchId } })
    fireEvent.click(screen.getByRole('button', { name: 'Mở batch' }))
    expect(await screen.findByText('fundus.png')).toBeTruthy()
    expect(screen.getByRole('status').textContent).toContain('Đã hoàn tất AI')
    expect(mocks.get).toHaveBeenCalledWith(batchId, context, expect.any(AbortSignal))
  })

  it('refreshing the browser URL only reads GET, no new receipt or upload', async () => {
    const first = show()
    await screen.findByText('fundus.png')
    first.unmount()
    show()
    await screen.findByText('fundus.png')
    expect(mocks.get).toHaveBeenCalledTimes(2)
  })

  it('terminal state stops polling after the initial GET', async () => {
    vi.useFakeTimers()
    show()
    await act(async () => { await Promise.resolve(); await Promise.resolve() })
    await act(async () => { await vi.advanceTimersByTimeAsync(60000) })
    expect(mocks.get).toHaveBeenCalledTimes(1)
  })

  it('HTTP 202 is never displayed as completed, then polling reads the actual batch', async () => {
    mocks.get.mockResolvedValueOnce({ kind: 'accepted' }).mockResolvedValue({ kind: 'batch', data: terminal })
    vi.useFakeTimers()
    show()
    await act(async () => { await Promise.resolve(); await Promise.resolve() })
    expect(screen.getByText(/Backend đã tiếp nhận batch/)).toBeTruthy()
    expect(screen.queryByText('fundus.png')).toBeNull()
    await act(async () => { await vi.advanceTimersByTimeAsync(3000) })
    expect(screen.getByText('fundus.png')).toBeTruthy()
  })

  it('temporary GET error backs off then recovers without any POST', async () => {
    mocks.get.mockRejectedValueOnce(new BatchProgressError(503, 'ERR_NETWORK', true))
      .mockResolvedValue({ kind: 'batch', data: terminal })
    vi.useFakeTimers()
    show()
    await act(async () => { await Promise.resolve(); await Promise.resolve() })
    expect(screen.getByText(/Mất kết nối hoặc lỗi GET tạm thời/)).toBeTruthy()
    await act(async () => { await vi.advanceTimersByTimeAsync(2000) })
    expect(screen.getByText('fundus.png')).toBeTruthy()
    expect(mocks.get).toHaveBeenCalledTimes(2)
  })

  it.each([[401, 'Phiên đăng nhập'], [403, 'quyền'], [404, 'Không tìm thấy batch']])(
    'shows a safe HTTP %s access message and hides data', async (status, expected) => {
      mocks.get.mockRejectedValueOnce(new BatchProgressError(status as number, 'ERR_FORBIDDEN', false))
      show()
      await waitFor(() => expect(screen.getByRole('alert').textContent).toContain(expected))
      expect(screen.queryByText('fundus.png')).toBeNull()
    },
  )

  it('reconnects with GET when the browser comes back online, with no duplicate request', async () => {
    const running: BatchProgress = {
      ...terminal, batchStatus: 'IN_PROGRESS',
      items: [{ ...terminal.items[0], processingStatus: 'PROCESSING', riskLevel: null }],
      summary: { total: 1, active: 1, succeeded: 0, failed: 0, duplicate: 0 },
    }
    mocks.get.mockResolvedValueOnce({ kind: 'batch', data: running })
      .mockResolvedValue({ kind: 'batch', data: terminal })
    show()
    await screen.findByText('fundus.png')
    act(() => { window.dispatchEvent(new Event('online')) })
    await waitFor(() => expect(mocks.get).toHaveBeenCalledTimes(2))
    expect(screen.getByRole('status').textContent).toContain('Đã hoàn tất AI')
  })

  it('aborts a pending GET when leaving the page', async () => {
    const controllerSignals: AbortSignal[] = []
    mocks.get.mockImplementation((_id, _context, signal: AbortSignal) => {
      controllerSignals.push(signal)
      return new Promise(() => {})
    })
    const page = show()
    expect(controllerSignals).toHaveLength(1)
    page.unmount()
    expect(controllerSignals[0].aborted).toBe(true)
  })
})
it('hiển thị đúng thông báo khi toàn bộ ảnh bị trùng', async () => {
  const duplicateBatch: BatchProgress = {
    ...terminal,
    items: [{
      ...terminal.items[0],
      processingStatus: 'DUPLICATE',
      riskLevel: null,
      existingAnalysisId: '60000000-0000-4000-8000-000000000006',
    }],
    summary: {
      total: 1,
      active: 0,
      succeeded: 0,
      failed: 0,
      duplicate: 1,
    },
  }

  mocks.get.mockResolvedValue({
    kind: 'batch',
    data: duplicateBatch,
  })

  show()

  expect(
    await screen.findByText(
      /Batch đã kết thúc — toàn bộ ảnh trùng, không có phân tích AI mới/
    )
  ).toBeTruthy()

  expect(screen.queryByText('Đã hoàn tất AI')).toBeNull()
})
