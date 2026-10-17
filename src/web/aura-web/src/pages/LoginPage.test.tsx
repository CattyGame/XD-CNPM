
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import LoginPage from './LoginPage'

// Giả lập Firebase, không cần kết nối Firebase thật
vi.mock('../firebase/config', () => ({
  getFirebaseAuth: () => null,
}))

describe('SCRUM-313 - LoginPage', () => {
  afterEach(() => {
    cleanup()
  })

  // TEST 1: Kiểm tra giao diện đăng nhập
  it('hiển thị giao diện đăng nhập', () => {
    render(
      <MemoryRouter>
        <LoginPage />
      </MemoryRouter>,
    )

    expect(screen.getByText('AURA — Đăng nhập')).toBeTruthy()
    expect(screen.getByLabelText('Email')).toBeTruthy()
    expect(screen.getByLabelText('Mật khẩu')).toBeTruthy()

    expect(
      screen.getByRole('button', { name: 'Đăng nhập bằng Email' }),
    ).toBeTruthy()

    expect(
      screen.getByRole('button', { name: 'Tiếp tục với Google' }),
    ).toBeTruthy()
  })

  // TEST 2: Kiểm tra xử lý lỗi khi Firebase chưa cấu hình
  it('hiển thị lỗi khi Firebase chưa được cấu hình', async () => {
    render(
      <MemoryRouter>
        <LoginPage />
      </MemoryRouter>,
    )

    // Nhập email
    fireEvent.change(screen.getByLabelText('Email'), {
      target: { value: 'test@example.com' },
    })

    // Nhập mật khẩu
    fireEvent.change(screen.getByLabelText('Mật khẩu'), {
      target: { value: '123456' },
    })

    // Nhấn nút đăng nhập
    fireEvent.click(
      screen.getByRole('button', { name: 'Đăng nhập bằng Email' }),
    )

    // Chờ thông báo lỗi xuất hiện
    const alert = await screen.findByRole('alert')

    // Kiểm tra nội dung thông báo
    expect(alert.textContent).toBe(
      'Đăng nhập thất bại. Vui lòng kiểm tra email và mật khẩu.',
    )
  })
})
