import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

describe('AURA connection check', () => {
  it('displays the service after a successful request', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        service: 'AURA.Analysis.Api',
        message: 'Template API is running',
        timestamp: '2026-10-06T10:00:00Z',
      }),
    })

    vi.stubGlobal('fetch', fetchMock)

    render(<App />)

    fireEvent.click(
      screen.getByRole('button', { name: 'Kiểm tra kết nối' }),
    )

    expect(await screen.findByText('AURA.Analysis.Api')).toBeTruthy()
    expect(fetchMock).toHaveBeenCalledWith('/api/v1/platform/ping')
  })

  it('displays an error when the gateway returns 502', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: false,
        status: 502,
      }),
    )

    render(<App />)

    fireEvent.click(
      screen.getByRole('button', { name: 'Kiểm tra kết nối' }),
    )

    expect((await screen.findByRole('alert')).textContent).toBe(
      'Không thể kết nối tới hệ thống.',
    )
  })
})
describe('AURA upload modal keyboard', () => {
  function openUploadModal() {
    render(<App />)

    const trigger = screen.getByRole('button', {
      name: /Tải dữ liệu lên.*Chọn tệp dữ liệu cần xử lý/,
    })

    trigger.focus()
    fireEvent.click(trigger)

    const dialog = screen.getByRole('dialog', {
      name: 'Tải dữ liệu lên',
    })

    return { trigger, dialog }
  }

  it('moves focus into the modal when opened', () => {
    const { dialog } = openUploadModal()

    expect(dialog.contains(document.activeElement)).toBe(true)
    expect(document.activeElement).toBe(
      screen.getByRole('button', { name: 'Đóng cửa sổ' }),
    )
  })

  it('wraps Tab and Shift+Tab inside the modal', () => {
    openUploadModal()

    const first = screen.getByRole('button', {
      name: 'Đóng cửa sổ',
    })
    const last = screen.getByRole('button', {
      name: 'Tiếp tục',
    })

    last.focus()
    fireEvent.keyDown(last, { key: 'Tab' })
    expect(document.activeElement).toBe(first)

    fireEvent.keyDown(first, {
      key: 'Tab',
      shiftKey: true,
    })
    expect(document.activeElement).toBe(last)
  })

  it('returns focus into the modal if background receives focus', () => {
    const { dialog } = openUploadModal()

    screen.getByRole('button', {
      name: 'Về Dashboard AURA',
    }).focus()

    expect(dialog.contains(document.activeElement)).toBe(true)
  })

  it('closes on Escape and restores focus to the trigger', () => {
    const { trigger, dialog } = openUploadModal()

    fireEvent.keyDown(dialog, { key: 'Escape' })

    expect(screen.queryByRole('dialog')).toBeNull()
    expect(document.activeElement).toBe(trigger)
  })

  it('restores focus after closing with the close button', () => {
    const { trigger } = openUploadModal()

    fireEvent.click(
      screen.getByRole('button', { name: 'Đóng cửa sổ' }),
    )

    expect(screen.queryByRole('dialog')).toBeNull()
    expect(document.activeElement).toBe(trigger)
  })
})