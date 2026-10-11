import { useEffect, useState } from 'react'
import {
  getNotificationPermission,
  requestNotificationPermission,
  type NotificationResult,
} from '../firebase/notificationPermission'
import { registerFirebaseServiceWorker } from '../firebase/serviceWorker'

function permissionGuidance(permission: NotificationResult) {
  switch (permission) {
    case 'denied':
      return 'Thông báo đã bị chặn. Mở cài đặt quyền của trang trong trình duyệt để cấp lại, rồi quay lại trang.'

    case 'unsupported':
      return 'Trình duyệt không hỗ trợ thông báo.'

    case 'granted':
      return 'Trình duyệt đã cấp quyền thông báo. Chưa xác nhận FCM token hoặc push thật.'

    default:
      return 'Chỉ yêu cầu quyền thông báo khi bạn nhấn Bật thông báo.'
  }
}

export default function NotificationSettings() {
  const [permission, setPermission] = useState<NotificationResult>(
    getNotificationPermission,
  )
  const [message, setMessage] = useState('')
  const [loading, setLoading] = useState(false)
  const [hasError, setHasError] = useState(false)

  const secureContext = window.isSecureContext === true

  useEffect(() => {
    function refreshPermission() {
      setPermission(getNotificationPermission())
      setMessage('')
      setHasError(false)
    }

    window.addEventListener('focus', refreshPermission)

    return () => {
      window.removeEventListener('focus', refreshPermission)
    }
  }, [])

  async function handleEnableNotification() {
    if (loading || !secureContext) return

    setLoading(true)
    setMessage('')
    setHasError(false)

    try {
      const result = await requestNotificationPermission()
      setPermission(result)

      if (result === 'granted') {
        const registration = await registerFirebaseServiceWorker()

        if (registration) {
          setMessage(
            'Đã cấp quyền và đăng ký service worker scaffold. Chưa xác nhận FCM token hoặc push thật.',
          )
        } else {
          setHasError(true)
          setMessage(
            'Đã cấp quyền nhưng chưa đăng ký được service worker. Kiểm tra hỗ trợ trình duyệt và thử lại.',
          )
        }
      } else {
        setMessage(permissionGuidance(result))
      }
    } catch {
      setHasError(true)
      setMessage('Không thể bật thông báo. Vui lòng thử lại.')
    } finally {
      setLoading(false)
    }
  }

  const guidance = secureContext
    ? permissionGuidance(permission)
    : 'Thông báo cần HTTPS hoặc localhost. Hãy mở ứng dụng trong môi trường phù hợp.'

  return (
    <section className="panel content-panel" style={{ marginTop: 20 }}>
      <h3>Cài đặt thông báo AURA</h3>

      <p>
        Trạng thái quyền: <strong>{permission}</strong>
      </p>

      <button
        type="button"
        className="button button-outline"
        onClick={handleEnableNotification}
        disabled={
          loading ||
          !secureContext ||
          permission === 'denied' ||
          permission === 'unsupported'
        }
      >
        {loading ? 'Đang xử lý...' : 'Bật thông báo'}
      </button>

      <p role={hasError ? 'alert' : 'status'}>
        {message || guidance}
      </p>

      <p className="section-description">
        Scaffold dùng nội dung thông báo chung. Chưa tích hợp đăng ký token
        với backend hoặc kiểm chứng gửi push thật.
      </p>
    </section>
  )
}