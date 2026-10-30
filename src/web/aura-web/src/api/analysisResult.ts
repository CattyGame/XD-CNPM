import { getFirebaseAuth } from '../firebase/config'
import { isMockEnabled } from './client'
import type { BackendContext } from './backendContext'
import { validBatchId } from './batchProgress'

/** SCRUM-333: GET Analysis REST v0.7. Không dùng HTTP 202 như kết quả AI. */
export type FileKind = 'original' | 'mask' | 'overlay' | 'heatmap' | 'annotated'
export type FileRef = Readonly<{ fileId: string; kind: FileKind }>
export type Warning = Readonly<{ code: string; message: string }>
export type AnalysisResult = Readonly<{
  version: number
  isMock: boolean
  riskAvailable: boolean
  riskLevel: 'LOW' | 'MODERATE' | 'HIGH' | null
  riskScore: number | null
  confidenceScore: number | null
  modelVersion: string
  configVersion: string
  thresholdVersion: string
  disclaimer: string
  limitations: string
  metrics: Readonly<Record<string, number | null>>
  indicators: readonly string[]
  annotations: readonly FileRef[]
  warnings: readonly Warning[]
}>
export type AnalysisDetail = Readonly<{
  analysisId: string
  batchId: string
  processingStatus: string
  reviewStatus: string
  updatedAt: string
  image: FileRef | null
  result: AnalysisResult | null
}>
export type PrivateFileAccess = Readonly<{ url: string; expiresAt: string }>

export class AnalysisResultError extends Error {
  readonly status: number
  readonly code: string
  readonly retryable: boolean

  constructor(status: number, code: string, retryable = false) {
    super(code)
    this.name = 'AnalysisResultError'
    this.status = status
    this.code = code
    this.retryable = retryable
  }
}

function record(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}
function invalid(): never { throw new AnalysisResultError(0, 'ERR_INVALID_RESPONSE') }
function string(value: unknown): value is string {
  return typeof value === 'string' && value.trim().length > 0
}
function date(value: unknown): value is string {
  return string(value) && Number.isFinite(Date.parse(value))
}
function fileRef(value: unknown): FileRef {
  if (!record(value) || !string(value.fileId) || !validBatchId(value.fileId) ||
    !['original', 'mask', 'overlay', 'heatmap', 'annotated'].includes(String(value.kind))) invalid()
  return { fileId: value.fileId, kind: value.kind as FileKind }
}
function riskValue(value: unknown): value is number {
  return typeof value === 'number' && Number.isFinite(value) && value >= 0 && value <= 1
}
function parseResult(value: unknown): AnalysisResult {
  if (!record(value) || !Number.isSafeInteger(value.version) || (value.version as number) < 1 ||
    typeof value.isMock !== 'boolean' || typeof value.riskAvailable !== 'boolean' ||
    !string(value.modelVersion) || !string(value.configVersion) ||
    !string(value.thresholdVersion) || typeof value.disclaimer !== 'string' ||
    typeof value.limitations !== 'string' || !record(value.metrics) ||
    !Array.isArray(value.indicators) || !value.indicators.every(string) ||
    !Array.isArray(value.annotations) || !Array.isArray(value.warnings)) invalid()

  // Risk không hỗ trợ => giữ null, không tạo điểm hoặc khuyến nghị giả.
  if (value.riskAvailable) {
    if (!['LOW', 'MODERATE', 'HIGH'].includes(String(value.riskLevel)) ||
      !riskValue(value.riskScore) || !riskValue(value.confidenceScore)) invalid()
  } else if (value.riskLevel !== null || value.riskScore !== null ||
    value.confidenceScore !== null || value.indicators.length !== 0) invalid()
  if (Object.values(value.metrics).some((v) => v !== null &&
    (typeof v !== 'number' || !Number.isFinite(v)))) invalid()
  const warnings: Warning[] = value.warnings.map((entry: unknown) => {
    if (!record(entry) || !string(entry.code) || !string(entry.message)) invalid()
    return { code: entry.code, message: entry.message }
  })
  return {
    version: value.version as number,
    isMock: value.isMock,
    riskAvailable: value.riskAvailable,
    riskLevel: value.riskLevel as AnalysisResult['riskLevel'],
    riskScore: value.riskScore as number | null,
    confidenceScore: value.confidenceScore as number | null,
    modelVersion: value.modelVersion,
    configVersion: value.configVersion,
    thresholdVersion: value.thresholdVersion,
    disclaimer: value.disclaimer,
    limitations: value.limitations,
    metrics: value.metrics as Record<string, number | null>,
    indicators: value.indicators as string[],
    annotations: value.annotations.map(fileRef),
    warnings,
  }
}

export function parseAnalysis(value: unknown, requestedId: string): AnalysisDetail {
  if (!record(value) || !validBatchId(requestedId) ||
    !string(value.analysisId) || value.analysisId.toLowerCase() !== requestedId.toLowerCase() ||
    !string(value.batchId) || !validBatchId(value.batchId) ||
    !string(value.processingStatus) || !string(value.reviewStatus) || !date(value.updatedAt)) invalid()
  const completed = value.processingStatus === 'COMPLETED'
  if (completed && !record(value.result)) invalid()
  if (!completed && value.result != null) invalid()
  const image = value.image == null ? null : fileRef(value.image)
  if (image && image.kind !== 'original') invalid()
  return {
    analysisId: value.analysisId,
    batchId: value.batchId,
    processingStatus: value.processingStatus,
    reviewStatus: value.reviewStatus,
    updatedAt: value.updatedAt,
    image,
    result: completed ? parseResult(value.result) : null,
  }
}

/** Từng GET cần token của phiên hiện tại, không cache và không ghi thông tin quyền vào URL. */
async function authorizedGet(path: string, context: BackendContext, signal?: AbortSignal): Promise<unknown> {
  if (isMockEnabled) throw new AnalysisResultError(0, 'ERR_MOCK_ENABLED')
  const auth = getFirebaseAuth()
  const user = auth?.currentUser
  if (!user || user.uid !== context.firebaseUid) throw new AnalysisResultError(401, 'ERR_SESSION_CHANGED')
  const current = () => {
    if (signal?.aborted) throw signal.reason
    if (auth.currentUser !== user || user.uid !== context.firebaseUid)
      throw new AnalysisResultError(401, 'ERR_SESSION_CHANGED')
  }
  const abort = signal
    ? AbortSignal.any([signal, AbortSignal.timeout(15000)])
    : AbortSignal.timeout(15000)
  try {
    current()
    const token = await user.getIdToken()
    current()
    if (!string(token) || /\s/.test(token)) throw new AnalysisResultError(401, 'ERR_TOKEN_UNAVAILABLE')
    const response = await fetch(path, {
      method: 'GET',
      headers: { Authorization: `Bearer ${token}` },
      credentials: 'omit', cache: 'no-store', redirect: 'error', signal: abort,
    })
    current()
    const payload: unknown = await response.json().catch(() => null)
    current()
    if (!response.ok) {
      const error = record(payload) && record(payload.error) ? payload.error : null
      const code = string(error?.code) && /^ERR_[A-Z0-9_]+$/.test(error.code)
        ? error.code : 'ERR_REQUEST_FAILED'
      throw new AnalysisResultError(response.status, code, response.status >= 500)
    }
    if (response.status !== 200) invalid()
    return payload
  } catch (cause) {
    if (cause instanceof AnalysisResultError) throw cause
    if (signal?.aborted) throw cause
    if (abort.aborted) throw new AnalysisResultError(0, 'ERR_TIMEOUT', true)
    throw new AnalysisResultError(0, 'ERR_NETWORK', true)
  }
}

export async function getAnalysisResult(
  analysisId: string, context: BackendContext, signal?: AbortSignal,
): Promise<AnalysisDetail> {
  if (!validBatchId(analysisId)) throw new AnalysisResultError(400, 'ERR_INVALID_ANALYSIS_ID')
  const payload = await authorizedGet(`/api/v1/analyses/${analysisId}`, context, signal)
  return parseAnalysis(payload, analysisId)
}

export async function getPrivateFileAccess(
  analysisId: string, fileId: string, context: BackendContext, signal?: AbortSignal,
): Promise<PrivateFileAccess> {
  if (!validBatchId(analysisId) || !validBatchId(fileId))
    throw new AnalysisResultError(400, 'ERR_INVALID_FILE_ID')
  const payload = await authorizedGet(
    `/api/v1/analyses/${analysisId}/files/${fileId}/access`, context, signal,
  )
  if (!record(payload) || !string(payload.url) || !date(payload.expiresAt)) invalid()
  let url: URL
  try { url = new URL(payload.url) } catch { return invalid() }
  if (!['http:', 'https:'].includes(url.protocol) ||
    (window.location.protocol === 'https:' && url.protocol !== 'https:') ||
    Date.parse(payload.expiresAt) <= Date.now() + 5000) invalid()
  // URL ký từ backend: dùng trực tiếp, không gắn Bearer token, không lưu lâu.
  return { url: payload.url, expiresAt: payload.expiresAt }
}

export function analysisErrorMessage(error: AnalysisResultError): string {
  if (error.status === 401) return 'Phiên đăng nhập đã hết hạn hoặc thay đổi. Hãy đăng nhập lại.'
  if (error.status === 403) return 'Vai trò hiện tại không được truy cập kết quả hoặc tệp ảnh này.'
  if (error.status === 404) return 'Không tìm thấy dữ liệu trong phạm vi được cấp quyền.'
  if (error.code === 'ERR_MOCK_ENABLED') return 'Đang bật mock. Tắt mock để dùng Analysis API thật.'
  if (error.code === 'ERR_INVALID_RESPONSE') return 'Phản hồi từ Analysis API không đúng hợp đồng.'
  if (error.code === 'ERR_TIMEOUT') return 'Yêu cầu quá thời gian. Có thể thử lại.'
  return 'Không tải được dữ liệu Analysis API. Vui lòng thử lại.'
}
