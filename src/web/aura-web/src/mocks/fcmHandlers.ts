import { delay, http, HttpResponse } from 'msw'

const demoEndpoint = '/api/v1/demo/fcm-token'

function demoError(
  status: number,
  code: string,
  message: string,
  retryable: boolean,
) {
  return HttpResponse.json(
    {
      error: {
        code,
        message,
        correlationId: 'c0ffee00-0000-4000-8000-000000000296',
        retryable,
      },
    },
    { status },
  )
}

export const fcmHandlers = [
  http.post(demoEndpoint, async ({ request }) => {
    const scenario = new URL(request.url).searchParams.get('scenario')

    switch (scenario) {
      case 'unauthorized':
        return demoError(
          401,
          'DEMO_FCM_UNAUTHORIZED',
          'Phiên đăng nhập không hợp lệ.',
          false,
        )

      case 'forbidden':
        return demoError(
          403,
          'DEMO_FCM_FORBIDDEN',
          'Không đủ quyền đăng ký thông báo.',
          false,
        )

      case 'server-error':
        return demoError(
          500,
          'DEMO_FCM_SERVER_ERROR',
          'Dịch vụ thông báo tạm thời không khả dụng.',
          true,
        )

      case 'timeout':
        await delay(10000)
        break

      case 'success':
        break

      default:
        return demoError(
          400,
          'DEMO_FCM_INVALID_SCENARIO',
          'Kịch bản demo không hợp lệ.',
          false,
        )
    }

    return HttpResponse.json(
      {
        registered: true,
        message: 'Đăng ký thông báo demo thành công. Chưa gửi push thật.',
      },
      { status: 200 },
    )
  }),
]