import { useEffect, useRef, useState } from 'react'
import { isMockEnabled } from '../api/client'
import { registerNotificationDevice, NotificationDeviceError } from '../api/notificationDevices'
import { sendNotificationTestPush } from '../api/notificationTestPush'
import { listenForegroundMessages, obtainFcmToken } from '../firebase/messaging'

type Props = { userId: string | null; parentBusy: boolean; onBusyChange: (busy: boolean) => void }

function errorMessage(error: unknown) {
  if (error instanceof NotificationDeviceError) {
    switch (error.code) {
      case 'ERR_RATE_LIMITED': return 'Đã đạt giới hạn gửi. Chờ một phút rồi thử lại.'
      case 'ERR_UNAUTHORIZED': return 'Phiên đăng nhập không hợp lệ. Đăng nhập lại.'
      case 'ERR_FCM_TOKEN_EXPIRED': return 'Token đã hết hiệu lực. Bật thông báo lại rồi thử gửi.'
      case 'ERR_DEVICE_NOT_REGISTERED': return 'Chưa có thiết bị của tài khoản. Bật thông báo lại.'
      case 'ERR_DEVICE_CONFLICT': return 'Không thể liên kết thiết bị. Đăng xuất tài khoản trước rồi thử lại.'
      case 'ERR_PUSH_TIMEOUT': return 'Chưa xác nhận kết quả gửi. Kiểm tra thông báo trước khi thử lại.'
    }
    if (error.status === 404) return 'Endpoint kiểm thử chưa bật. Kiểm tra cấu hình Notification API.'
  }
  return 'Không gửi được thông báo kiểm thử. Kiểm tra kết nối và cấu hình rồi thử lại.'
}

function waitForSend(signal: AbortSignal) {
  return new Promise<void>((resolve, reject) => {
    signal.throwIfAborted()
    const abort = () => { window.clearTimeout(timer); reject(new DOMException('Aborted', 'AbortError')) }
    const timer = window.setTimeout(() => {
      signal.removeEventListener('abort', abort)
      resolve()
    }, 5000)
    signal.addEventListener('abort', abort, { once: true })
  })
}

function NotificationPushTestSession({ userId, parentBusy, onBusyChange }: Props) {
  const [sending, setSending] = useState(false)
  const [message, setMessage] = useState('')
  const [receipt, setReceipt] = useState('')
  const [listenerReady, setListenerReady] = useState(false)
  const [hasError, setHasError] = useState(false)
  const operation = useRef<AbortController | null>(null)
  const active = import.meta.env.DEV && !isMockEnabled && window.isSecureContext === true && Boolean(userId)

  useEffect(() => {
    let disposed = false
    let unsubscribe: (() => void) | null = null
    if (active) {
      void listenForegroundMessages((payload) => {
        if (disposed) return
        const id = payload.data?.testId
        const safeId = payload.data?.kind === 'aura-test' && typeof id === 'string' && /^[a-f0-9]{32}$/.test(id) ? id : null
        setReceipt(safeId
          ? `Đã nhận thông báo foreground. Mã kiểm thử: ${safeId}`
          : 'Bạn có thông báo mới từ AURA. Mở hệ thống để xem.')
      }).then((stop) => {
        if (disposed) stop?.()
        else { unsubscribe = stop; setListenerReady(Boolean(stop)) }
      }).catch(() => {
        if (!disposed) setListenerReady(false)
      })
    }
    return () => {
      disposed = true
      unsubscribe?.()
      if (operation.current) {
        operation.current.abort()
        operation.current = null
        onBusyChange(false)
      }
    }
  }, [active, userId, onBusyChange])

  async function send(delayed: boolean) {
    if (!active || !userId || parentBusy || operation.current || !listenerReady) return
    if (!('Notification' in window) || Notification.permission !== 'granted') {
      setHasError(true)
      setMessage('Bật thông báo và cấp quyền trước khi gửi kiểm thử.')
      return
    }
    const controller = new AbortController()
    operation.current = controller
    setSending(true)
    onBusyChange(true)
    setHasError(false)
    setMessage('Đang chuẩn bị gửi...')
    setReceipt('')
    try {
      const token = await obtainFcmToken()
      controller.signal.throwIfAborted()
      if (token.status !== 'ready') throw new Error('Token unavailable')
      await registerNotificationDevice(token.token)
      controller.signal.throwIfAborted()
      if (delayed) {
        setMessage('Chuyển sang tab khác ngay. Thông báo sẽ được gửi sau 5 giây.')
        await waitForSend(controller.signal)
      }
      const result = await sendNotificationTestPush(userId, controller.signal)
      controller.signal.throwIfAborted()
      setMessage(`FCM đã chấp nhận gửi. Mã kiểm thử: ${result.testId}. Chờ nhận thông báo.`)
    } catch (error) {
      if (!controller.signal.aborted) { setHasError(true); setMessage(errorMessage(error)) }
    } finally {
      if (operation.current === controller) {
        operation.current = null
        setSending(false)
        onBusyChange(false)
      }
    }
  }

  if (!import.meta.env.DEV) return null
  return (
    <div aria-label="Kiểm thử push thật">
      <h4>Kiểm thử push thật</h4>
      <p>{listenerReady ? 'Sẵn sàng nhận thông báo foreground.' : 'Đăng nhập và bật thông báo để chuẩn bị nhận.'}</p>
      <button type="button" className="button button-outline" disabled={!active || parentBusy || sending || !listenerReady} onClick={() => void send(false)}>
        Gửi push kiểm thử
      </button>
      <button type="button" className="button button-outline" disabled={!active || parentBusy || sending || !listenerReady} onClick={() => void send(true)}>
        Gửi sau 5 giây
      </button>
      {message && <p role={hasError ? 'alert' : 'status'}>{message}</p>}
      {receipt && <p role="status">{receipt}</p>}
    </div>
  )
}


export default function NotificationPushTestPanel(props: Props) {
  const sessionKey = `${props.userId ?? 'signed-out'}:${isMockEnabled}:${window.isSecureContext === true}`

  return <NotificationPushTestSession key={sessionKey} {...props} />
}
