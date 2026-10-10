export const isMockEnabled =
  import.meta.env.DEV &&
  import.meta.env.VITE_ENABLE_MOCK === 'true'

const API_BASE_URL = isMockEnabled
  ? ''
  : (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/+$/, '')

type ErrorPayload = {
  error?: {
    code?: string
    message?: string
    correlationId?: string
    retryable?: boolean
  }
}

export class ApiError extends Error {
  readonly status: number
  readonly code: string
  readonly correlationId?: string
  readonly retryable: boolean

  constructor(status: number, payload: ErrorPayload | null) {
    super(payload?.error?.message ?? `HTTP ${status}`)

    this.name = 'ApiError'
    this.status = status
    this.code = payload?.error?.code ?? 'UNKNOWN_ERROR'
    this.correlationId = payload?.error?.correlationId
    this.retryable = payload?.error?.retryable ?? false
  }
}

async function apiRequest<T>(
  path: string,
  method: 'GET' | 'POST',
  body?: unknown,
): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method,
    signal: AbortSignal.timeout(3000),
    ...(body === undefined
      ? {}
      : {
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(body),
        }),
  })

  if (!response.ok) {
    const payload: ErrorPayload | null = await response
      .json()
      .catch(() => null)

    throw new ApiError(response.status, payload)
  }

  return response.json() as Promise<T>
}

export function apiGet<T = unknown>(path: string): Promise<T> {
  return apiRequest<T>(path, 'GET')
}

export function apiPost<T = unknown>(
  path: string,
  body?: unknown,
): Promise<T> {
  return apiRequest<T>(path, 'POST', body)
}
