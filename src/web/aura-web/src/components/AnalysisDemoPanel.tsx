import { useState } from 'react'
import { ApiError, apiGet, apiPost, isMockEnabled } from '../api/client'

type Scenario =
  | 'create'
  | 'validation'
  | 'insufficient-credit'
  | 'empty-list'
  | 'processing'
  | 'completed'
  | 'failed'
  | 'quality-reject'
  | '401'
  | '403'
  | 'timeout'

type AnalysisResponse = {
  processingStatus: string
  reviewStatus: string
  result?: {
    riskLevel: string
    isMock: boolean
  }
  error?: {
    message: string
    retryable: boolean
  }
}

type AnalysisList = {
  items: unknown[]
  total: number
}

type CreateResponse = {
  batchId: string
}

const analysisId = '11111111-1111-4111-8111-111111111111'

function recoveryMessage(error: ApiError) {
  switch (error.status) {
    case 400:
      return 'Kiểm tra dữ liệu và chọn ảnh hợp lệ trước khi gửi lại.'
    case 401:
      return 'Đăng nhập lại trước khi tiếp tục.'
    case 402:
      return 'Kiểm tra số dư hoặc gói dịch vụ trước khi tạo yêu cầu.'
    case 403:
      return 'Quay về khu vực được phép hoặc liên hệ người quản lý.'
    default:
      return error.retryable
        ? 'Có thể thử lại yêu cầu.'
        : 'Kiểm tra nguyên nhân trước khi gửi lại.'
  }
}

export default function AnalysisDemoPanel() {
  const [scenario, setScenario] = useState<Scenario>('processing')
  const [loading, setLoading] = useState(false)
  const [message, setMessage] = useState('')
  const [hasError, setHasError] = useState(false)

  async function runScenario() {
    if (!isMockEnabled || loading) return

    setLoading(true)
    setMessage('')
    setHasError(false)

    try {
      if (
        scenario === 'create' ||
        scenario === 'validation' ||
        scenario === 'insufficient-credit'
      ) {
        const query = scenario === 'create'
          ? ''
          : `?scenario=${scenario}`

        const data = await apiPost<CreateResponse>(
          `/api/v1/analyses${query}`,
        )

        setMessage(
          `Đã tiếp nhận yêu cầu giả lập. Batch: ${data.batchId}. ` +
          'Chưa có kết quả phân tích.',
        )
        return
      }

      if (scenario === 'empty-list') {
        const data = await apiGet<AnalysisList>('/api/v1/analyses')

        setMessage(
          data.items.length === 0
            ? 'Chưa có hồ sơ phân tích. Hãy tạo yêu cầu mới.'
            : `Có ${data.total} hồ sơ phân tích.`,
        )
        return
      }

      const data = await apiGet<AnalysisResponse>(
        `/api/v1/analyses/${analysisId}?scenario=${scenario}`,
      )

      switch (data.processingStatus) {
        case 'PROCESSING':
          setMessage('Đang phân tích. Chưa có kết quả nguy cơ.')
          break

        case 'COMPLETED':
          if (!data.result) {
            setHasError(true)
            setMessage('Phản hồi hoàn tất nhưng thiếu kết quả. Hãy kiểm tra dữ liệu.')
            break
          }

          setMessage(
            `Phân tích hoàn tất. Nguy cơ minh họa: ${data.result.riskLevel}. ` +
            `Doctor review: ${data.reviewStatus}. ` +
            `Nguồn: ${data.result.isMock ? 'MOCK' : 'không gắn nhãn mock'}.`,
          )
          break

        case 'FAILED':
          setHasError(true)
          setMessage(
            `Phân tích thất bại. ${data.error?.message ?? ''} ` +
            'Chưa có kết quả nguy cơ. ' +
            (data.error?.retryable
              ? 'Có thể thử lại theo chính sách retry.'
              : 'Kiểm tra nguyên nhân trước khi gửi lại.'),
          )
          break

        case 'LOW_QUALITY':
          setHasError(true)
          setMessage(
            'Ảnh bị từ chối trong scenario giả lập. ' +
            'Hãy chọn ảnh khác. Chưa có kết quả nguy cơ; ' +
            'demo này không đánh giá chất lượng ảnh thật.',
          )
          break

        default:
          setMessage(
            `Trạng thái: ${data.processingStatus}. Chưa hiển thị kết quả nguy cơ.`,
          )
      }
    } catch (error) {
      setHasError(true)

      if (error instanceof ApiError) {
        setMessage(
          `HTTP ${error.status} — ${error.code}: ${error.message} ` +
          recoveryMessage(error),
        )
      } else if (
        error instanceof Error &&
        (error.name === 'TimeoutError' || error.name === 'AbortError')
      ) {
        setMessage(
          'Yêu cầu đã bị hủy do hết thời gian chờ. Hãy thử lại. ' +
          'Chưa có kết quả nguy cơ.',
        )
      } else {
        setMessage(
          'Không thể kết nối. Kiểm tra mạng và MSW, sau đó thử lại. ' +
          'Chưa có kết quả nguy cơ.',
        )
      }
    } finally {
      setLoading(false)
    }
  }

  if (!isMockEnabled) return null

  return (
    <section className="panel content-panel">
      <h2>Demo Analysis — Dữ liệu giả lập</h2>

      <p>
        Kiểm tra giao diện bằng MSW. Không tải ảnh thật, không xác thực
        quyền backend và không dùng để chẩn đoán.
      </p>

      <label htmlFor="analysis-demo-scenario">Kịch bản Analysis</label>

      <select
        id="analysis-demo-scenario"
        value={scenario}
        disabled={loading}
        onChange={(event) => {
          setScenario(event.target.value as Scenario)
          setMessage('')
          setHasError(false)
        }}
      >
        <option value="create">Tạo yêu cầu — 202</option>
        <option value="validation">Dữ liệu không hợp lệ — 400</option>
        <option value="insufficient-credit">Thiếu credit — 402</option>
        <option value="empty-list">Danh sách trống</option>
        <option value="processing">Đang phân tích</option>
        <option value="completed">Phân tích hoàn tất</option>
        <option value="failed">Phân tích thất bại</option>
        <option value="quality-reject">Ảnh bị từ chối giả lập</option>
        <option value="401">Chưa xác thực — 401</option>
        <option value="403">Không đủ quyền — 403</option>
        <option value="timeout">Hết thời gian chờ</option>
      </select>

      <button
        type="button"
        className="button button-primary"
        disabled={loading}
        onClick={runScenario}
      >
        {loading ? 'Đang chạy demo...' : 'Chạy demo Analysis'}
      </button>

      {message && (
        <p role={hasError ? 'alert' : 'status'}>
          {message}
        </p>
      )}
    </section>
  )
}
