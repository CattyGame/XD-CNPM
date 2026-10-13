// @vitest-environment jsdom

import { afterAll, afterEach, beforeAll, describe, expect, it } from 'vitest'
import { setupServer } from 'msw/node'
import { handlers } from './handlers'

const server = setupServer(...handlers)
const analysisId = '11111111-1111-4111-8111-111111111111'

function apiUrl(path: string) {
  return new URL(path, window.location.origin).href
}

beforeAll(() => {
  server.listen({ onUnhandledRequest: 'error' })
})

afterEach(() => {
  server.resetHandlers()
})

afterAll(() => {
  server.close()
})

describe('Analysis MSW handlers', () => {
  it('POST analyses trả 202 và thông tin yêu cầu', async () => {
    const response = await fetch(apiUrl('/api/v1/analyses'), {
      method: 'POST',
    })

    expect(response.status).toBe(202)

    const body = await response.json()

    expect(body).toHaveProperty('batchId')
    expect(body).toHaveProperty('statusUrl')
  })

  it('POST validation trả 400 và ERR_VALIDATION', async () => {
    const response = await fetch(
      apiUrl('/api/v1/analyses?scenario=validation'),
      { method: 'POST' },
    )

    expect(response.status).toBe(400)

    const body = await response.json()

    expect(body.error.code).toBe('ERR_VALIDATION')
    expect(body.error.retryable).toBe(false)
  })

  it('thiếu credit trả 402 và Error đúng cấu trúc', async () => {
    const response = await fetch(
      apiUrl('/api/v1/analyses?scenario=insufficient-credit'),
      { method: 'POST' },
    )

    expect(response.status).toBe(402)

    const body = await response.json()

    expect(body.error.code).toBe('ERR_INSUFFICIENT_CREDIT')
    expect(body.error.retryable).toBe(false)
    expect(body.error.correlationId).toEqual(expect.any(String))
    expect(body.error.message).toEqual(expect.any(String))
  })

  it.each([
    { scenario: '401', status: 401, code: 'ERR_UNAUTHORIZED' },
    { scenario: '403', status: 403, code: 'ERR_FORBIDDEN' },
  ])('GET scenario $scenario trả lỗi $code', async ({ scenario, status, code }) => {
    const response = await fetch(
      apiUrl(`/api/v1/analyses/${analysisId}?scenario=${scenario}`),
    )

    expect(response.status).toBe(status)

    const body = await response.json()

    expect(body.error.code).toBe(code)
    expect(body.error.correlationId).toEqual(expect.any(String))
  })

  it('GET processing chưa trả kết quả nguy cơ', async () => {
    const response = await fetch(
      apiUrl(`/api/v1/analyses/${analysisId}?scenario=processing`),
    )

    expect(response.status).toBe(200)

    const body = await response.json()

    expect(body.processingStatus).toBe('PROCESSING')
    expect(body.job.attemptNo).toBeGreaterThanOrEqual(1)
    expect(body.result ?? null).toBeNull()
  })

  it('GET analysis hoàn tất trả kết quả có version', async () => {
    const response = await fetch(
      apiUrl(`/api/v1/analyses/${analysisId}?scenario=completed`),
    )

    expect(response.status).toBe(200)

    const body = await response.json()

    expect(body.processingStatus).toBe('COMPLETED')
    expect(body.result.version).toBeGreaterThanOrEqual(1)
    expect(body.result.isMock).toBe(true)
    expect(body.job.attemptNo).toBeGreaterThanOrEqual(1)
  })

  it.each([
    { scenario: 'failed', status: 'FAILED' },
    { scenario: 'quality-reject', status: 'LOW_QUALITY' },
  ])('GET $scenario không tạo kết quả nguy cơ', async ({ scenario, status }) => {
    const response = await fetch(
      apiUrl(`/api/v1/analyses/${analysisId}?scenario=${scenario}`),
    )

    expect(response.status).toBe(200)

    const body = await response.json()

    expect(body.processingStatus).toBe(status)
    expect(body.error.code).toEqual(expect.any(String))
    expect(body.result ?? null).toBeNull()
  })

  it('GET analyses trả danh sách trống có phân trang', async () => {
    const response = await fetch(apiUrl('/api/v1/analyses'))

    expect(response.status).toBe(200)
    expect(await response.json()).toEqual({
      items: [],
      page: 1,
      pageSize: 20,
      total: 0,
    })
  })
})