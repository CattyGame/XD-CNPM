import { useState } from 'react'
import { ApiError, isMockEnabled } from '../api/client'
import {
  registerFcmDemo,
  type FcmDemoScenario,
} from '../api/fcmDemo'

export default function FcmDemoPanel() {
  const [scenario, setScenario] = useState<FcmDemoScenario>('success')
  const [result, setResult] = useState('')
  const [loading, setLoading] = useState(false)
  const [hasError, setHasError] = useState(false)

  async function handleTest() {
    if (!isMockEnabled || loading) return

    setLoading(true)
    setResult('')
    setHasError(false)

    try {
      setResult(await registerFcmDemo(scenario))
    } catch (error) {
      setHasError(true)

      if (error instanceof ApiError) {
        let recovery = 'Hãy thử lại sau.'

        if (error.status === 401) {
          recovery = 'Đăng nhập lại trước khi tiếp tục.'
        } else if (error.status === 403) {
          recovery = 'Liên hệ quản trị viên để kiểm tra quyền truy cập.'
        }

        setResult(
          `HTTP ${error.status} — ${error.code}: ${error.message} ${recovery}`,
        )
      } else if (
        error instanceof Error &&
        (error.name === 'TimeoutError' || error.name === 'AbortError')
      ) {
        setResult(
          'Yêu cầu demo đã hết thời gian chờ. Hãy thử lại. Chưa xác nhận đăng ký thông báo.',
        )
      } else {
        setResult(
          'Không thể hoàn tất request demo. Kiểm tra kết nối và MSW rồi thử lại.',
        )
      }
    } finally {
      setLoading(false)
    }
  }

  if (!isMockEnabled) return null

  return (
    <section className="panel content-panel" style={{ marginTop: 20 }}>
      <h3>FCM API Demo — Dữ liệu giả lập</h3>

      <p className="section-description">
        Mô phỏng phản hồi API bằng MSW. Không gửi token thật hoặc push thật.
        Kết quả demo không chứng minh kết nối Firebase hay phân quyền backend.
      </p>

      <label htmlFor="fcm-scenario">Kịch bản FCM demo</label>

      <select
        id="fcm-scenario"
        value={scenario}
        disabled={loading}
        onChange={(event) => {
          setScenario(event.target.value as FcmDemoScenario)
          setResult('')
          setHasError(false)
        }}
        style={{ display: 'block', margin: '12px 0', padding: 10 }}
      >
        <option value="success">200 — Thành công giả lập</option>
        <option value="unauthorized">401 — Chưa xác thực</option>
        <option value="forbidden">403 — Không đủ quyền</option>
        <option value="server-error">500 — Lỗi server</option>
        <option value="timeout">Hết thời gian chờ</option>
      </select>

      <button
        type="button"
        className="button button-primary"
        onClick={handleTest}
        disabled={loading}
      >
        {loading ? 'Đang kiểm thử...' : 'Chạy demo FCM'}
      </button>

      {result && (
        <p role={hasError ? 'alert' : 'status'} style={{ marginTop: 12 }}>
          {result}
        </p>
      )}
    </section>
  )
}