import { apiPost, isMockEnabled } from './client'

export type FcmDemoScenario =
  | 'success'
  | 'unauthorized'
  | 'forbidden'
  | 'server-error'
  | 'timeout'

type FcmDemoResponse = {
  registered: boolean
  message: string
}

export async function registerFcmDemo(
  scenario: FcmDemoScenario,
): Promise<string> {
  if (!isMockEnabled) {
    throw new Error('Demo FCM chỉ hoạt động khi bật mock ở development.')
  }

  const response = await apiPost<FcmDemoResponse>(
    `/api/v1/demo/fcm-token?scenario=${encodeURIComponent(scenario)}`,
    { demo: true },
  )

  if (response.registered !== true) {
    throw new Error('API demo chưa xác nhận đăng ký.')
  }

  return `200 — ${response.message}`
}