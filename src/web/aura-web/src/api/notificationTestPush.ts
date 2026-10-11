import { isMockEnabled } from './client'
import { getNotificationIdToken } from '../firebase/auth'
import { getFirebaseAuth } from '../firebase/config'
import { getOrCreateDeviceId, NotificationDeviceError } from './notificationDevices'

export async function sendNotificationTestPush(uid: string, signal: AbortSignal) {
  if (!import.meta.env.DEV || isMockEnabled) {
    throw new NotificationDeviceError(0, 'ERR_TEST_PUSH_DISABLED')
  }
  const matchesUser = () => getFirebaseAuth()?.currentUser?.uid === uid
  if (!matchesUser()) throw new NotificationDeviceError(401, 'ERR_UNAUTHORIZED')
  const idToken = await getNotificationIdToken()
  signal.throwIfAborted()
  if (!matchesUser()) throw new NotificationDeviceError(401, 'ERR_UNAUTHORIZED')
  const timeout = new AbortController()
  const abort = () => timeout.abort()
  signal.addEventListener('abort', abort, { once: true })
  const timer = window.setTimeout(abort, 20000)
  try {
    signal.throwIfAborted()
    const deviceId = getOrCreateDeviceId()
    const response = await fetch(`/api/v1/notifications/devices/${deviceId}/test-push`, {
      method: 'POST', headers: { Authorization: `Bearer ${idToken}` },
      signal: timeout.signal, cache: 'no-store', credentials: 'omit', redirect: 'error',
    })
    const payload: unknown = await response.json().catch(() => null)
    if (!response.ok) {
      let code = 'ERR_PUSH_UNAVAILABLE'
      if (payload && typeof payload === 'object' && 'error' in payload) {
        const error = payload.error
        if (error && typeof error === 'object' && 'code' in error && typeof error.code === 'string') {
          code = error.code
        }
      }
      throw new NotificationDeviceError(response.status, code)
    }
    if (!payload || typeof payload !== 'object' || !('accepted' in payload) || payload.accepted !== true
      || !('testId' in payload) || typeof payload.testId !== 'string' || !/^[a-f0-9]{32}$/.test(payload.testId)) {
      throw new NotificationDeviceError(0, 'ERR_INVALID_RESPONSE')
    }
    return { accepted: true as const, testId: payload.testId }
  } catch (error) {
    if (signal.aborted) throw error
    if (error instanceof NotificationDeviceError) throw error
    throw new NotificationDeviceError(0, timeout.signal.aborted ? 'ERR_PUSH_TIMEOUT' : 'ERR_NETWORK')
  } finally {
    window.clearTimeout(timer)
    signal.removeEventListener('abort', abort)
  }
}
