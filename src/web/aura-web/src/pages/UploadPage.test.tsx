
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import UploadPage from './UploadPage'

// Không kết nối Firebase thật khi kiểm thử
const { mockAuth } = vi.hoisted(() => ({
  mockAuth: {
    currentUser: null as null | {
      getIdToken: () => Promise<string>
    },
  },
}))

vi.mock('../firebase/config', () => ({
  getFirebaseAuth: () => mockAuth,
}))

describe('SCRUM-313 - UploadPage', () => {
  afterEach(() => {
  cleanup()
  mockAuth.currentUser = null
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

  it('hiển thị đầy đủ giao diện Upload', () => {
    render(<UploadPage />)

    expect(
      screen.getByText('AURA — Upload ảnh võng mạc'),
    ).toBeTruthy()

    expect(
      screen.getByLabelText('Patient ID (UUID)'),
    ).toBeTruthy()

    expect(
      screen.getByLabelText('Mắt được chụp'),
    ).toBeTruthy()

    expect(
      screen.getByLabelText('Chọn ảnh võng mạc'),
    ).toBeTruthy()

    expect(
      screen.getByRole('button', { name: 'Gửi ảnh phân tích' }),
    ).toBeTruthy()
  })

it('từ chối file không phải JPG hoặc PNG', async () => {
  render(<UploadPage />)

  // Tạo file PDF giả lập
  const invalidFile = new File(
    ['fake pdf content'],
    'document.pdf',
    { type: 'application/pdf' },
  )

  // Giả lập người dùng chọn file
  const fileInput = screen.getByLabelText(
    'Chọn ảnh võng mạc',
  ) as HTMLInputElement

  fireEvent.change(fileInput, {
    target: { files: [invalidFile] },
  })

  // Kiểm tra thông báo lỗi
  const status = await screen.findByRole('status')

  expect(status.textContent).toBe(
    'File "document.pdf" không hợp lệ. Chỉ hỗ trợ JPG và PNG.',
  )

  // Không có ảnh hợp lệ được chọn
  expect(
    screen.queryByText('Đã chọn 1 ảnh:'),
  ).toBeNull()
})

it('từ chối khi chọn quá 100 ảnh', async () => {
  render(<UploadPage />)

  // Tạo 101 ảnh JPEG giả lập
  const files = Array.from({ length: 101 }, (_, index) =>
    new File(
      ['fake image'],
      `image-${index + 1}.jpg`,
      { type: 'image/jpeg' },
    ),
  )

  const fileInput = screen.getByLabelText(
    'Chọn ảnh võng mạc',
  ) as HTMLInputElement

  // Giả lập chọn 101 ảnh cùng lúc
  fireEvent.change(fileInput, {
    target: { files },
  })

  // Kiểm tra hệ thống từ chối
  const status = await screen.findByRole('status')

  expect(status.textContent).toBe(
    'Mỗi lần chỉ được chọn tối đa 100 ảnh.',
  )

  // Không hiển thị danh sách ảnh đã chọn
  expect(screen.queryByText(/Đã chọn \d+ ảnh:/)).toBeNull()
})

it('từ chối ảnh có dung lượng vượt quá 10 MB', async () => {
  render(<UploadPage />)

  // Giả lập trình duyệt đọc kích thước ảnh
  const createImageBitmapMock = vi.fn().mockResolvedValue({
    width: 1000,
    height: 1000,
    close: vi.fn(),
  })

  vi.stubGlobal('createImageBitmap', createImageBitmapMock)

  // Tạo ảnh JPEG giả lập có dung lượng 11 MB
  const largeFile = new File(
    [new Uint8Array(11 * 1024 * 1024)],
    'large-image.jpg',
    { type: 'image/jpeg' },
  )

  const fileInput = screen.getByLabelText(
    'Chọn ảnh võng mạc',
  ) as HTMLInputElement

  fireEvent.change(fileInput, {
    target: { files: [largeFile] },
  })

  // Kiểm tra thông báo lỗi
  const status = await screen.findByRole('status')

  expect(status.textContent).toBe(
    'Mỗi ảnh không được vượt quá 10 MB.',
  )

  expect(
    screen.queryByText('Đã chọn 1 ảnh:'),
  ).toBeNull()

  vi.unstubAllGlobals()
})

it('từ chối ảnh vượt quá 16 triệu pixel', async () => {
  render(<UploadPage />)

  // Giả lập ảnh có độ phân giải 5000 x 4000
  // Tổng cộng 20 triệu pixel
  const closeMock = vi.fn()

  vi.stubGlobal(
    'createImageBitmap',
    vi.fn().mockResolvedValue({
      width: 5000,
      height: 4000,
      close: closeMock,
    }),
  )

  // Tạo file JPEG giả lập
  const largeResolutionFile = new File(
    ['fake image'],
    'high-resolution.jpg',
    { type: 'image/jpeg' },
  )

  const fileInput = screen.getByLabelText(
    'Chọn ảnh võng mạc',
  ) as HTMLInputElement

  fireEvent.change(fileInput, {
    target: { files: [largeResolutionFile] },
  })

  // Kiểm tra thông báo lỗi
  const status = await screen.findByRole('status')

  expect(status.textContent).toBe(
    'Ảnh "high-resolution.jpg" vượt quá giới hạn 16 triệu pixel.',
  )

  // Kiểm tra tài nguyên ảnh được giải phóng
  expect(closeMock).toHaveBeenCalled()

  // Không lưu ảnh không hợp lệ
  expect(screen.queryByText('Đã chọn 1 ảnh:')).toBeNull()

  vi.unstubAllGlobals()
})

it('từ chối Patient ID không đúng định dạng UUID', async () => {
  render(<UploadPage />)

  // Nhập Patient ID sai định dạng
  fireEvent.change(screen.getByLabelText('Patient ID (UUID)'), {
    target: { value: 'patient123' },
  })

  // Nhấn nút gửi ảnh
  fireEvent.click(
    screen.getByRole('button', { name: 'Gửi ảnh phân tích' }),
  )

  // Kiểm tra thông báo lỗi
  const status = await screen.findByRole('status')

  expect(status.textContent).toBe(
    'Patient ID phải là UUID hợp lệ.',
  )
})

it('yêu cầu chọn ít nhất một ảnh trước khi gửi', async () => {
  render(<UploadPage />)

  // Nhập UUID hợp lệ
  fireEvent.change(screen.getByLabelText('Patient ID (UUID)'), {
    target: {
      value: '550e8400-e29b-41d4-a716-446655440000',
    },
  })

  // Không chọn ảnh, nhấn nút gửi
  fireEvent.click(
    screen.getByRole('button', { name: 'Gửi ảnh phân tích' }),
  )

  // Kiểm tra thông báo lỗi
  const status = await screen.findByRole('status')

  expect(status.textContent).toBe(
    'Vui lòng chọn ít nhất một ảnh.',
  )
})

it('từ chối gửi ảnh khi chưa đăng nhập Firebase', async () => {
  render(<UploadPage />)

  // Giả lập trình duyệt đọc ảnh hợp lệ
  vi.stubGlobal(
    'createImageBitmap',
    vi.fn().mockResolvedValue({
      width: 1000,
      height: 1000,
      close: vi.fn(),
    }),
  )

  vi.stubGlobal(
    'URL',
    Object.assign(URL, {
      createObjectURL: vi.fn(() => 'blob:mock-image'),
      revokeObjectURL: vi.fn(),
    }),
  )

  // Nhập UUID hợp lệ
  fireEvent.change(screen.getByLabelText('Patient ID (UUID)'), {
    target: {
      value: '550e8400-e29b-41d4-a716-446655440000',
    },
  })

  // Chọn ảnh JPEG hợp lệ
  const image = new File(
    ['fake image'],
    'retina.jpg',
    { type: 'image/jpeg' },
  )

  fireEvent.change(screen.getByLabelText('Chọn ảnh võng mạc'), {
    target: { files: [image] },
  })

  // Chờ ảnh được xử lý và hiển thị
  expect(
    await screen.findByText('Đã chọn 1 ảnh:'),
  ).toBeTruthy()

  // Nhấn nút gửi ảnh
  fireEvent.click(
    screen.getByRole('button', { name: 'Gửi ảnh phân tích' }),
  )

  // Firebase mock trả về null nên chưa đăng nhập
  const status = await screen.findByRole('status')

  expect(status.textContent).toBe(
    'Bạn cần đăng nhập trước khi tải ảnh.',
  )

  vi.unstubAllGlobals()
})

it('hiển thị kết quả khi Backend trả về 202 Accepted', async () => {
  // Giả lập người dùng đã đăng nhập
  mockAuth.currentUser = {
    getIdToken: vi.fn().mockResolvedValue('fake-test-token'),
  }

  // Giả lập ảnh hợp lệ
  vi.stubGlobal(
    'createImageBitmap',
    vi.fn().mockResolvedValue({
      width: 1000,
      height: 1000,
      close: vi.fn(),
    }),
  )

  vi.stubGlobal(
    'URL',
    Object.assign(URL, {
      createObjectURL: vi.fn(() => 'blob:mock-image'),
      revokeObjectURL: vi.fn(),
    }),
  )

  // Giả lập Backend trả về 202
  const fetchMock = vi.fn().mockResolvedValue({
    ok: true,
    status: 202,
    json: async () => ({
      batchId: 'batch-test-001',
      items: [{}],
      createdAt: '2026-10-17T10:00:00Z',
      statusUrl: '/api/v1/analyses/batch-test-001',
    }),
  })

  vi.stubGlobal('fetch', fetchMock)

  render(<UploadPage />)

  fireEvent.change(screen.getByLabelText('Patient ID (UUID)'), {
    target: {
      value: '550e8400-e29b-41d4-a716-446655440000',
    },
  })

  const image = new File(
    ['fake image'],
    'retina.jpg',
    { type: 'image/jpeg' },
  )

  fireEvent.change(screen.getByLabelText('Chọn ảnh võng mạc'), {
    target: { files: [image] },
  })

  await screen.findByText('Đã chọn 1 ảnh:')

  fireEvent.click(
    screen.getByRole('button', { name: 'Gửi ảnh phân tích' }),
  )

  // Kiểm tra kết quả tiếp nhận
  expect(
    await screen.findByText(
      'Backend đã tiếp nhận ảnh và tạo yêu cầu phân tích.',
    ),
  ).toBeTruthy()

  expect(screen.getByText('batch-test-001')).toBeTruthy()
  expect(screen.getByText('Thông tin tiếp nhận')).toBeTruthy()

  // Kiểm tra đã gọi API
  expect(fetchMock).toHaveBeenCalledTimes(1)

  const [url, options] = fetchMock.mock.calls[0]

  expect(url).toContain('/api/v1/analyses')
  expect(options.method).toBe('POST')
  expect(options.headers.Authorization).toBe('Bearer fake-test-token')
  expect(options.body).toBeInstanceOf(FormData)

  // Khôi phục mock sau bài test
  mockAuth.currentUser = null
  vi.unstubAllGlobals()
})

it('hiển thị lỗi khi Backend trả về HTTP 401', async () => {
  // Giả lập người dùng đã đăng nhập Firebase
  mockAuth.currentUser = {
    getIdToken: vi.fn().mockResolvedValue('fake-test-token'),
  }

  // Giả lập ảnh hợp lệ
  vi.stubGlobal(
    'createImageBitmap',
    vi.fn().mockResolvedValue({
      width: 1000,
      height: 1000,
      close: vi.fn(),
    }),
  )

  vi.stubGlobal(
    'URL',
    Object.assign(URL, {
      createObjectURL: vi.fn(() => 'blob:mock-image'),
      revokeObjectURL: vi.fn(),
    }),
  )

  // Backend giả lập từ chối xác thực
  const fetchMock = vi.fn().mockResolvedValue({
    ok: false,
    status: 401,
  })

  vi.stubGlobal('fetch', fetchMock)

  render(<UploadPage />)

  fireEvent.change(screen.getByLabelText('Patient ID (UUID)'), {
    target: {
      value: '550e8400-e29b-41d4-a716-446655440000',
    },
  })

  const image = new File(
    ['fake image'],
    'retina.jpg',
    { type: 'image/jpeg' },
  )

  fireEvent.change(screen.getByLabelText('Chọn ảnh võng mạc'), {
    target: { files: [image] },
  })

  await screen.findByText('Đã chọn 1 ảnh:')

  fireEvent.click(
    screen.getByRole('button', { name: 'Gửi ảnh phân tích' }),
  )

  // Kiểm tra thông báo lỗi
  const status = await screen.findByRole('status')

  expect(status.textContent).toBe(
    'Phiên đăng nhập không hợp lệ hoặc đã hết hạn.',
  )

  expect(fetchMock).toHaveBeenCalledTimes(1)
})

it.each([
  [400, 'Dữ liệu gửi lên không hợp lệ.'],
  [403, 'Bạn không có quyền tải ảnh cho bệnh nhân này.'],
  [413, 'Dung lượng ảnh hoặc yêu cầu vượt quá giới hạn.'],
  [429, 'Bạn gửi quá nhiều yêu cầu. Vui lòng thử lại sau.'],
  [500, 'Máy chủ đang gặp sự cố. Vui lòng thử lại sau.'],
  [502, 'Máy chủ đang gặp sự cố. Vui lòng thử lại sau.'],
  [503, 'Máy chủ đang gặp sự cố. Vui lòng thử lại sau.'],
])('hiển thị đúng lỗi HTTP %i', async (httpStatus, expectedMessage) => {
  mockAuth.currentUser = {
    getIdToken: vi.fn().mockResolvedValue('fake-test-token'),
  }

  vi.stubGlobal(
    'createImageBitmap',
    vi.fn().mockResolvedValue({
      width: 1000,
      height: 1000,
      close: vi.fn(),
    }),
  )

  vi.stubGlobal(
    'URL',
    Object.assign(URL, {
      createObjectURL: vi.fn(() => 'blob:mock-image'),
      revokeObjectURL: vi.fn(),
    }),
  )

  const fetchMock = vi.fn().mockResolvedValue({
    ok: false,
    status: httpStatus,
  })

  vi.stubGlobal('fetch', fetchMock)

  render(<UploadPage />)

  fireEvent.change(screen.getByLabelText('Patient ID (UUID)'), {
    target: {
      value: '550e8400-e29b-41d4-a716-446655440000',
    },
  })

  const image = new File(
    ['fake image'],
    'retina.jpg',
    { type: 'image/jpeg' },
  )

  fireEvent.change(screen.getByLabelText('Chọn ảnh võng mạc'), {
    target: { files: [image] },
  })

  await screen.findByText('Đã chọn 1 ảnh:')

  fireEvent.click(
    screen.getByRole('button', { name: 'Gửi ảnh phân tích' }),
  )

  const status = await screen.findByRole('status')

  expect(status.textContent).toBe(expectedMessage)
  expect(fetchMock).toHaveBeenCalledTimes(1)
})

})
