import { useBackendSession } from './backendSession'
import type { BackendContextError } from '../api/backendContext'

function message(error: BackendContextError): string {
  if (error.code === 'ERR_PROFILE_NOT_READY') return 'Hồ sơ đang được khởi tạo. Vui lòng thử lại sau.'
  if (error.code === 'ERR_FIREBASE_NOT_CONFIGURED') return 'Dịch vụ đăng nhập chưa được cấu hình.'
  if (error.code === 'ERR_ACCOUNT_DISABLED') return 'Tài khoản AURA đã bị khóa. Vui lòng liên hệ quản trị viên.'
  if (error.code === 'ERR_INVALID_RESPONSE') return 'Thông tin tài khoản hoặc hồ sơ không hợp lệ. Vui lòng liên hệ quản trị viên.'
  if (error.code === 'ERR_MOCK_ENABLED') return 'Không thể xác nhận tài khoản trong chế độ demo.'
  if (error.code === 'ERR_SIGN_OUT_FAILED') return 'Không thể đăng xuất. Vui lòng thử lại.'
  if (error.status === 401) return 'Phiên đăng nhập không hợp lệ hoặc đã hết hạn. Vui lòng đăng nhập lại.'
  if (error.status === 403) return 'Tài khoản chưa được phép truy cập AURA.'
  return 'Chưa thể xác nhận tài khoản và hồ sơ. Vui lòng thử lại.'
}

export default function BackendSessionNotice() {
  const { state, retry, logout } = useBackendSession()
  if (state.status === 'loading') return <p role="status">Đang xác nhận tài khoản và hồ sơ AURA...</p>
  if (state.status !== 'error') return null

  return (
    <section aria-label="Trạng thái tài khoản AURA">
      <p role="alert">{message(state.error)}</p>
      {state.error.correlationId && <p>Mã đối chiếu: {state.error.correlationId}</p>}
      {state.error.retryable && state.error.status !== 401 && state.error.status !== 403 && (
        <button type="button" onClick={retry}>Thử lại xác nhận hồ sơ</button>
      )}
      {state.firebaseUid && (
        <button type="button" onClick={() => void logout()}>Đăng xuất / đổi tài khoản</button>
      )}
    </section>
  )
}
