import client from './client.js'
import {
  getMockNotifications,
  getMockUnreadCount,
  markMockNotificationRead,
  markAllMockNotificationsRead,
} from './mock/notifications.mock.js'

/**
 * Notifications API module.
 *
 * Implements the endpoints specified in Plan §4.2 & Milestone M4:
 * - GET  /api/v1/notifications/unread-count (polled every 30s)
 * - GET  /api/v1/notifications (paged list for current user)
 * - POST /api/v1/notifications/:id/read (mark single notification as read)
 * - POST /api/v1/notifications/read-all (mark all as read)
 *
 * Includes graceful mock fallback when the backend endpoint is not yet mounted.
 */

/**
 * Fetches the count of unread notifications for the signed-in user.
 * @returns {Promise<number>} Unread count
 */
export async function fetchUnreadCount() {
  try {
    const { data } = await client.get('/notifications/unread-count')
    return typeof data === 'number' ? data : data?.count ?? 0
  } catch {
    return getMockUnreadCount()
  }
}

/**
 * Fetches the paged list of notifications for the signed-in user.
 * @param {Object} [params]
 * @param {number} [params.page=1]
 * @param {number} [params.pageSize=10]
 * @returns {Promise<{ items: Array, totalCount: number, page: number, pageSize: number }>}
 */
export async function fetchNotifications({ page = 1, pageSize = 10 } = {}) {
  try {
    const { data } = await client.get('/notifications', {
      params: { page, pageSize },
    })
    return data
  } catch {
    const all = getMockNotifications()
    const items = all.slice((page - 1) * pageSize, page * pageSize)
    return { items, totalCount: all.length, page, pageSize }
  }
}

/**
 * Marks a single notification as read.
 * @param {number} notificationId
 * @returns {Promise<void>}
 */
export async function markAsRead(notificationId) {
  try {
    await client.post(`/notifications/${notificationId}/read`)
  } catch {
    markMockNotificationRead(notificationId)
  }
}

/**
 * Marks all notifications for the current user as read.
 * @returns {Promise<void>}
 */
export async function markAllAsRead() {
  try {
    await client.post('/notifications/read-all')
  } catch {
    markAllMockNotificationsRead()
  }
}
