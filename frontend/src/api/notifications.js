import client from './client.js'

/**
 * Notification feed API client (Plan §4.2, "Notifications — Member 4").
 * Backed by WebApi/Controllers/NotificationsController.cs.
 */

/** Paged feed, newest first. */
export async function getNotifications({ page = 1, pageSize = 20 } = {}) {
  return (await client.get('/notifications', { params: { page, pageSize } })).data
}

/** Polled every 30s by useNotifications — must stay a cheap single indexed COUNT server-side. */
export async function getUnreadCount() {
  return (await client.get('/notifications/unread-count')).data
}

export async function markNotificationRead(notificationId) {
  await client.post(`/notifications/${notificationId}/read`)
}

export async function markAllNotificationsRead() {
  await client.post('/notifications/read-all')
}
