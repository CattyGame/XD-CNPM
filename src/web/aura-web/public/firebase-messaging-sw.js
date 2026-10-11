/*
 * SCRUM-296 - AURA notification Service Worker scaffold.
 * Thông báo chỉ hiển thị nội dung chung.
 * Chưa xác minh luồng FCM background thật.
 */

self.addEventListener('push', function (event) {
  event.waitUntil(
    self.registration.showNotification('AURA - Thông báo mới', {
      body: 'Bạn có thông báo mới. Mở AURA để xem chi tiết.',
      tag: 'aura-notification',
    }),
  )
})

self.addEventListener('notificationclick', function (event) {
  event.notification.close()

  event.waitUntil(
    self.clients.openWindow('/'),
  )
})