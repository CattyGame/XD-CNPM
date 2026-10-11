import NotificationPushTestPanel from './NotificationPushTestPanel'
import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import type { User } from 'firebase/auth'
import { isMockEnabled } from '../api/client'
import {
  NotificationDeviceError,

  unregisterNotificationDevice,
} from '../api/notificationDevices'
import {
  loginForNotifications,
  logoutFirebaseNotificationSession,
  observeNotificationUser,
} from '../firebase/auth'
import {
  getNotificationPermission,
  requestNotificationPermission,
  type NotificationResult,
} from '../firebase/notificationPermission'

import { clearFcmOnLogout, refreshFcmToken } from '../firebase/tokenLifecycle'

function permissionGuidance(permission: NotificationResult) {
  switch (permission) {
    case 'denied':
      return 'Thông báo đã bị chặn. Mở cài đặt quyền của trang trong trình duyệt để cấp lại.'

    case 'unsupported':
      return 'Trình duyệt không hỗ trợ thông báo.'

    case 'granted':
      return 'Đã cấp quyền. Nhấn Bật thông báo để đăng ký thiết bị.'

    default:
      return 'Chỉ yêu cầu quyền thông báo khi bạn nhấn Bật thông báo.'
  }
}

function deviceErrorMessage(error: unknown) {
  if (error instanceof NotificationDeviceError) {
    if (error.status === 401) {
      return 'Phiên đăng nhập không hợp lệ. Hãy đăng xuất rồi đăng nhập lại.'
    }

    if (error.status === 409) {
      return 'Không thể liên kết thiết bị với tài khoản hiện tại. Cần hủy liên kết ở tài khoản đã đăng ký trước.'
    }

    if (error.code === 'ERR_MOCK_ENABLED') {
      return 'Tắt chế độ mock và khởi động lại Vite để đăng ký thiết bị thật.'
    }

    if (error.status === 503 || error.code === 'ERR_NETWORK') {
      return 'Chưa xác nhận được đăng ký thiết bị. Kiểm tra kết nối rồi thử lại.'
    }
  }

  return 'Không hoàn thành được thao tác thông báo. Vui lòng thử lại.'
}

export default function NotificationSettings() {
  const [user, setUser] = useState<User | null>(null)
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [permission, setPermission] = useState<NotificationResult>(
    getNotificationPermission,
  )
  const [message, setMessage] = useState('')
  const [loading, setLoading] = useState(false)
  const [hasError, setHasError] = useState(false)

  const [syncEnabled, setSyncEnabled] = useState(false)
  const operationInFlight = useRef(false)
  const pushInFlight = useRef(false)
  const sessionUid = useRef<string | null>(null)

  const handlePushBusyChange = useCallback((busy: boolean) => {
    pushInFlight.current = busy
    setLoading(operationInFlight.current || busy)
  }, [])

  const secureContext = window.isSecureContext === true

  useEffect(() => {
    return observeNotificationUser((nextUser) => {
      const nextUid = nextUser?.uid ?? null

      if (sessionUid.current !== nextUid) {
        setSyncEnabled(false)
      }

      sessionUid.current = nextUid
      setUser(nextUser)
    })
  }, [])

  useEffect(() => {
    let active = true

    async function refreshPermission() {
      const nextPermission = getNotificationPermission()
      setPermission(nextPermission)

      if (nextPermission !== 'granted') {
        setSyncEnabled(false)
        return
      }

      if (
        !syncEnabled ||
        !user ||
        isMockEnabled ||
        !secureContext ||
        operationInFlight.current ||
        pushInFlight.current
      ) {
        return
      }

      operationInFlight.current = true
      setLoading(true)

      try {
        const result = await refreshFcmToken(
          () =>
            active &&
            sessionUid.current === user.uid &&
            getNotificationPermission() === 'granted',
        )

        if (!active || result.status === 'cancelled') {
          return
        }

        if (result.status === 'ready') {
          setHasError(false)
          setMessage('Đã đồng bộ đăng ký thông báo với backend.')
        } else {
          setHasError(true)
          setMessage(
            'Chưa đồng bộ được thông báo. Kiểm tra quyền, cấu hình và kết nối rồi thử lại.',
          )
        }
      } catch (error) {
        if (active) {
          setHasError(true)
          setMessage(deviceErrorMessage(error))
        }
      } finally {
        operationInFlight.current = false
        setLoading(pushInFlight.current)
      }
    }

    function handleFocus() {
      void refreshPermission()
    }

    function handleVisibility() {
      if (document.visibilityState === 'visible') {
        void refreshPermission()
      }
    }

    window.addEventListener('focus', handleFocus)
    document.addEventListener('visibilitychange', handleVisibility)

    return () => {
      active = false
      window.removeEventListener('focus', handleFocus)
      document.removeEventListener('visibilitychange', handleVisibility)
    }
  }, [user, syncEnabled, secureContext])

  async function handleLogin(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (operationInFlight.current || pushInFlight.current || loading || isMockEnabled) {
      return
    }

    operationInFlight.current = true
    setLoading(true)
    setMessage('')
    setHasError(false)

    try {
      const signedInUser = await loginForNotifications(email, password)

      sessionUid.current = signedInUser.uid
      setSyncEnabled(false)
      setUser(signedInUser)
      setMessage('Đăng nhập Firebase thành công. Có thể bật thông báo.')
    } catch {
      setHasError(true)
      setMessage(
        'Đăng nhập thất bại. Kiểm tra tài khoản Firebase và kết nối rồi thử lại.',
      )
    } finally {
      setPassword('')
      operationInFlight.current = false
      setLoading(pushInFlight.current)
    }
  }

  async function handleEnableNotification() {
    if (
      operationInFlight.current ||
      pushInFlight.current ||
      loading ||
      !user ||
      isMockEnabled ||
      !secureContext ||
      permission === 'denied' ||
      permission === 'unsupported'
    ) {
      return
    }

    operationInFlight.current = true
    setLoading(true)
    setMessage('')
    setHasError(false)

    try {
      const nextPermission = await requestNotificationPermission()
      setPermission(nextPermission)

      if (nextPermission !== 'granted') {
        setMessage(permissionGuidance(nextPermission))
        return
      }

      const result = await refreshFcmToken(
        () => sessionUid.current === user.uid,
      )

      if (result.status === 'cancelled') {
        return
      }

      if (result.status !== 'ready') {
        setHasError(true)

        switch (result.status) {
          case 'not-configured':
            setMessage(
              'Chưa có cấu hình Firebase/VAPID hợp lệ. Kiểm tra cấu hình và khởi động lại Vite.',
            )
            break

          case 'permission-required':
            setPermission(getNotificationPermission())
            setMessage('Chưa được cấp quyền thông báo.')
            break

          case 'unsupported':
            setMessage('Môi trường hiện tại không hỗ trợ FCM.')
            break

          default:
            setMessage('Không thể lấy FCM token. Kiểm tra kết nối rồi thử lại.')
            break
        }

        return
      }



      setSyncEnabled(true)

      setMessage(
        'Đã đăng ký thiết bị với backend. Chưa kiểm chứng gửi và nhận push thật.',
      )
    } catch (error) {
      setHasError(true)
      setMessage(deviceErrorMessage(error))
    } finally {
      operationInFlight.current = false
      setLoading(pushInFlight.current)
    }
  }

  async function handleLogout() {
    if (operationInFlight.current || pushInFlight.current || loading || !user || isMockEnabled) {
      return
    }

    operationInFlight.current = true
    setLoading(true)
    setMessage('')
    setHasError(false)

    try {
      await unregisterNotificationDevice()
      setSyncEnabled(false)

      const result = await clearFcmOnLogout()

      if (result.status === 'error') {
        setHasError(true)
        setMessage(
          'Đã hủy liên kết backend nhưng chưa xóa được FCM token. Nhấn Đăng xuất để thử lại.',
        )
        return
      }

      await logoutFirebaseNotificationSession()

      sessionUid.current = null
      setUser(null)
      setPassword('')
      setMessage('Đã hủy liên kết thiết bị và đăng xuất.')
    } catch {
      setHasError(true)
      setMessage(
        'Chưa hoàn tất hủy liên kết thiết bị. Phiên đăng nhập được giữ để bạn thử lại.',
      )
    } finally {
      operationInFlight.current = false
      setLoading(pushInFlight.current)
    }
  }

  return (
    <section className="panel content-panel" style={{ marginTop: 20 }}>
      <h3>Cài đặt thông báo AURA</h3>

      {isMockEnabled && (
        <p>Tắt chế độ mock để sử dụng đăng nhập và thông báo thật.</p>
      )}

      {user ? (
        <div>
          <p>Đã đăng nhập: {user.email ?? 'Tài khoản Firebase'}</p>

          <button
            type="button"
            className="button button-outline"
            onClick={handleLogout}
            disabled={loading || isMockEnabled}
          >
            Đăng xuất
          </button>
        </div>
      ) : (
        <form onSubmit={handleLogin}>
          <div>
            <label htmlFor="notification-email">Email Firebase</label>
            <input
              id="notification-email"
              type="email"
              autoComplete="username"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              disabled={loading || isMockEnabled}
              required
            />
          </div>

          <div>
            <label htmlFor="notification-password">Mật khẩu Firebase</label>
            <input
              id="notification-password"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              disabled={loading || isMockEnabled}
              required
            />
          </div>

          <button
            type="submit"
            className="button button-outline"
            disabled={loading || isMockEnabled}
          >
            Đăng nhập thông báo
          </button>
        </form>
      )}

      <p>
        Trạng thái quyền: <strong>{permission}</strong>
      </p>

      <button
        type="button"
        className="button button-outline"
        onClick={handleEnableNotification}
        disabled={
          loading ||
          !user ||
          isMockEnabled ||
          !secureContext ||
          permission === 'denied' ||
          permission === 'unsupported'
        }
      >
        {loading ? 'Đang xử lý...' : 'Bật thông báo'}
      </button>

      <p role={hasError ? 'alert' : 'status'}>
        {message || (
          !secureContext
            ? 'Thông báo cần HTTPS hoặc localhost.'
            : !user
              ? 'Đăng nhập Firebase trước khi bật thông báo.'
              : permissionGuidance(permission)
        )}
      </p>

      <NotificationPushTestPanel userId={user?.uid ?? null} parentBusy={loading} onBusyChange={handlePushBusyChange} />

      <p className="section-description">
        Phiên Firebase phục vụ kiểm thử thông báo, chưa cấp quyền nghiệp vụ
        AURA. Đăng ký thiết bị thành công chưa xác nhận nhận push.
      </p>
    </section>
  )
}
