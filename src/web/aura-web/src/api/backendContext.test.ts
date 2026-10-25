import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const mocks = vi.hoisted(() => ({ getAuth: vi.fn(), mockEnabled: false }))
vi.mock('../firebase/config', () => ({ getFirebaseAuth: mocks.getAuth }))
vi.mock('./client', () => ({ get isMockEnabled() { return mocks.mockEnabled } }))
import { BackendContextError, loadBackendContext } from './backendContext'

const accountId = '10000000-0000-0000-0000-000000000001'
const patientId = '20000000-0000-0000-0000-000000000002'
const tenantId = '30000000-0000-0000-0000-000000000003'
const account = {
  accountId, firebaseUid: 'firebase-user-a', accountStatus: 'ACTIVE',
  roles: ['PATIENT'], profileProvisioningStatus: 'READY',
}
const profile = {
  accountId, patientId, personalTenantId: tenantId,
  displayName: 'Hồ sơ A', phone: null, profileVersion: 1,
}
const user = { uid: account.firebaseUid, getIdToken: vi.fn() }
const auth: { currentUser: typeof user | null } = { currentUser: user }
const fetchMock = vi.fn<typeof fetch>()

function json(payload: unknown, status = 200): Response {
  return { ok: status >= 200 && status < 300, status, json: async () => payload } as Response
}

function success(a: unknown = account, p: unknown = profile) {
  fetchMock.mockResolvedValueOnce(json(a)).mockResolvedValueOnce(json(p))
}

beforeEach(() => {
  vi.resetAllMocks()
  mocks.mockEnabled = false
  user.uid = account.firebaseUid
  user.getIdToken.mockResolvedValue('test-id-token')
  auth.currentUser = user
  mocks.getAuth.mockReturnValue(auth)
  vi.stubGlobal('fetch', fetchMock)
})
afterEach(() => vi.unstubAllGlobals())

describe('loadBackendContext', () => {
  it('uses bootstrap then Profile with Firebase bearer and no identity overrides', async () => {
    success()
    const result = await loadBackendContext()
    expect(result).toEqual({
      accountId, firebaseUid: account.firebaseUid, roles: ['PATIENT'],
      patientId, personalTenantId: tenantId, displayName: profile.displayName, profileVersion: 1,
    })
    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      '/api/v1/identity/bootstrap', '/api/v1/profiles/me',
    ])
    for (const [index, [, options]] of fetchMock.mock.calls.entries()) {
      expect(options).toMatchObject({
        method: index === 0 ? 'POST' : 'GET',
        headers: { Authorization: 'Bearer test-id-token' },
        cache: 'no-store', credentials: 'omit', redirect: 'error',
      })
      expect(options?.body).toBeUndefined()
      expect(options?.signal).toBeDefined()
    }
    expect(Object.isFrozen(result)).toBe(true)
    expect(Object.isFrozen(result.roles)).toBe(true)
    expect(JSON.stringify(result)).not.toContain('test-id-token')
    expect(user.getIdToken).toHaveBeenCalledTimes(1)
  })

  it('preserves multiple backend roles and normalizes UUID casing', async () => {
    const upper = 'ABCDEFAB-0000-0000-0000-000000000001'
    success({ ...account, accountId: upper, roles: ['DOCTOR', 'PATIENT', 'DOCTOR'] },
      { ...profile, accountId: upper.toLowerCase() })
    expect(await loadBackendContext()).toMatchObject({
      accountId: upper.toLowerCase(), roles: ['DOCTOR', 'PATIENT'],
    })
  })

  it('blocks mock mode before accessing Firebase or HTTP', async () => {
    mocks.mockEnabled = true
    await expect(loadBackendContext()).rejects.toMatchObject({ code: 'ERR_MOCK_ENABLED' })
    expect(mocks.getAuth).not.toHaveBeenCalled()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it.each([null, { currentUser: null }])('requires an authenticated user: %j', async (value) => {
    mocks.getAuth.mockReturnValue(value)
    await expect(loadBackendContext()).rejects.toMatchObject({ status: 401, code: 'ERR_UNAUTHORIZED' })
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it.each(['', 'bad token', undefined])('rejects unusable tokens: %s', async (value) => {
    user.getIdToken.mockResolvedValue(value)
    await expect(loadBackendContext()).rejects.toMatchObject({ code: 'ERR_TOKEN_UNAVAILABLE' })
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('sanitizes Firebase failure without exposing provider messages', async () => {
    user.getIdToken.mockRejectedValue(new Error('private-provider-details'))
    const error = await loadBackendContext().catch((e) => e)
    expect(error).toBeInstanceOf(BackendContextError)
    expect(error.code).toBe('ERR_TOKEN_UNAVAILABLE')
    expect(error.message).not.toContain('private-provider-details')
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('returns retryable PENDING without requesting Profile or retrying bootstrap', async () => {
    success({ ...account, profileProvisioningStatus: 'PENDING' })
    await expect(loadBackendContext()).rejects.toMatchObject({
      status: 409, code: 'ERR_PROFILE_NOT_READY', retryable: true,
    })
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('blocks a disabled account even when HTTP returns 200', async () => {
    success({ ...account, accountStatus: 'DISABLED' })
    await expect(loadBackendContext()).rejects.toMatchObject({ status: 403, code: 'ERR_ACCOUNT_DISABLED' })
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it.each([
    null, [], {}, { ...account, accountId: 'firebase-uid-is-not-account-id' },
    { ...account, accountId: '00000000-0000-0000-0000-000000000000' },
    { ...account, firebaseUid: 'other-user' }, { ...account, roles: [] },
    { ...account, roles: ['ADMIN'] }, { ...account, roles: ['__proto__'] },
    { ...account, roles: ['PATIENT', 1] }, { ...account, roles: 'PATIENT' },
    { ...account, profileProvisioningStatus: 'UNKNOWN' },
    { ...account, accountStatus: null },
  ])('rejects invalid bootstrap payload %j', async (value) => {
    success(value)
    await expect(loadBackendContext()).rejects.toMatchObject({ code: 'ERR_INVALID_RESPONSE' })
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it.each([
    null, [], {}, { ...profile, accountId: tenantId },
    { ...profile, patientId: null }, { ...profile, personalTenantId: null },
    { ...profile, patientId: '00000000-0000-0000-0000-000000000000' },
    { ...profile, personalTenantId: 'not-a-uuid' },
    { ...profile, displayName: '  ' }, { ...profile, displayName: null },
    { ...profile, profileVersion: 0 }, { ...profile, profileVersion: 1.5 },
    { ...profile, profileVersion: '1' }, { ...profile, profileVersion: Number.MAX_SAFE_INTEGER + 1 },
  ])('rejects invalid or mismatched Profile payload %j', async (value) => {
    success(account, value)
    await expect(loadBackendContext()).rejects.toMatchObject({ code: 'ERR_INVALID_RESPONSE' })
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it.each([401, 403, 409, 500, 503])('preserves HTTP %i and safe error metadata without retry', async (status) => {
    fetchMock.mockResolvedValueOnce(json({ error: {
      code: 'ERR_PROFILE_NOT_READY', retryable: true, correlationId: 'request-123',
      message: 'private-server-message',
    } }, status))
    const error = await loadBackendContext().catch((e) => e)
    expect(error).toMatchObject({ status, code: 'ERR_PROFILE_NOT_READY', retryable: true, correlationId: 'request-123' })
    expect(error.message).not.toContain('private-server-message')
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('propagates Profile not-ready response without returning partial context', async () => {
    fetchMock.mockResolvedValueOnce(json(account)).mockResolvedValueOnce(json({ error: {
      code: 'ERR_PROFILE_NOT_READY', retryable: true,
    } }, 409))
    await expect(loadBackendContext()).rejects.toMatchObject({ status: 409, code: 'ERR_PROFILE_NOT_READY' })
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it('handles non-JSON success as an invalid response', async () => {
    fetchMock.mockResolvedValueOnce(new Response('invalid-json', { status: 200 }))
    await expect(loadBackendContext()).rejects.toMatchObject({ code: 'ERR_INVALID_RESPONSE' })
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('handles non-JSON HTTP failures with their original status', async () => {
    fetchMock.mockResolvedValueOnce(new Response('invalid-json', { status: 502 }))
    await expect(loadBackendContext()).rejects.toMatchObject({ status: 502, code: 'ERR_REQUEST_FAILED' })
  })

  it('rejects unsafe or wrongly typed error metadata', async () => {
    fetchMock.mockResolvedValueOnce(json({ error: {
      code: 'test-id-token', retryable: 'true', correlationId: '<script>',
    } }, 500))
    await expect(loadBackendContext()).rejects.toMatchObject({
      status: 500, code: 'ERR_REQUEST_FAILED', retryable: false, correlationId: undefined,
    })
  })

  it('sanitizes network failure and makes no automatic retry', async () => {
    fetchMock.mockRejectedValueOnce(new Error('private-network-details'))
    await expect(loadBackendContext()).rejects.toMatchObject({ status: 0, code: 'ERR_NETWORK', retryable: true })
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('rejects logout during token retrieval before sending HTTP', async () => {
    user.getIdToken.mockImplementation(async () => { auth.currentUser = null; return 'old-token' })
    await expect(loadBackendContext()).rejects.toMatchObject({ code: 'ERR_SESSION_CHANGED' })
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('rejects account switch after bootstrap before requesting Profile', async () => {
    fetchMock.mockImplementationOnce(async () => {
      auth.currentUser = { ...user, uid: 'firebase-user-b' }
      return json(account)
    })
    await expect(loadBackendContext()).rejects.toMatchObject({ code: 'ERR_SESSION_CHANGED' })
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('rejects logout while decoding Profile response', async () => {
    fetchMock.mockResolvedValueOnce(json(account)).mockResolvedValueOnce({
      ok: true, status: 200, json: async () => { auth.currentUser = null; return profile },
    } as Response)
    await expect(loadBackendContext()).rejects.toMatchObject({ code: 'ERR_SESSION_CHANGED' })
  })

  it('rejects an already cancelled load without retrieving a token', async () => {
    const controller = new AbortController()
    controller.abort()
    await expect(loadBackendContext(controller.signal)).rejects.toMatchObject({ code: 'ERR_CANCELLED' })
    expect(user.getIdToken).not.toHaveBeenCalled()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('cancels between requests and does not load Profile', async () => {
    const controller = new AbortController()
    fetchMock.mockImplementationOnce(async () => { controller.abort(); return json(account) })
    await expect(loadBackendContext(controller.signal)).rejects.toMatchObject({ code: 'ERR_CANCELLED' })
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('forwards caller cancellation to the in-flight fetch', async () => {
    const controller = new AbortController()
    fetchMock.mockImplementationOnce((_url, options) => new Promise((_resolve, reject) => {
      options?.signal?.addEventListener('abort', () => reject(new DOMException('cancelled', 'AbortError')))
      controller.abort()
    }))
    await expect(loadBackendContext(controller.signal)).rejects.toMatchObject({ code: 'ERR_CANCELLED' })
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('does not reuse previous successful context or token on a new load', async () => {
    success()
    await loadBackendContext()
    user.getIdToken.mockResolvedValue('new-token')
    fetchMock.mockResolvedValueOnce(json({ error: { code: 'ERR_ACCOUNT_DISABLED' } }, 403))
    await expect(loadBackendContext()).rejects.toMatchObject({ status: 403, code: 'ERR_ACCOUNT_DISABLED' })
    expect(fetchMock).toHaveBeenCalledTimes(3)
    expect(fetchMock.mock.calls[2][1]?.headers).toEqual({ Authorization: 'Bearer new-token' })
  })
})
