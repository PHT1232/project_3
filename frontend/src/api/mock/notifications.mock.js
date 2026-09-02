/**
 * Mock notifications for local development & fallback when backend is offline.
 * Matches Plan §3.4 dual-party trigger events.
 */
let mockStore = [
  {
    notificationId: 1,
    recipientEmployeeNumber: 1,
    requestId: 101,
    eventType: 'RequestApproved',
    title: 'Request Approved',
    message: 'Your request #101 for "Standard A4 Copy Paper" has been approved by your manager.',
    isRead: false,
    createdAtUtc: new Date(Date.now() - 5 * 60 * 1000).toISOString(), // 5 mins ago
  },
  {
    notificationId: 2,
    recipientEmployeeNumber: 1,
    requestId: 102,
    eventType: 'RequestSubmitted',
    title: 'New Request to Review',
    message: 'Staff submitted request #102 with 3 items awaiting your approval.',
    isRead: false,
    createdAtUtc: new Date(Date.now() - 45 * 60 * 1000).toISOString(), // 45 mins ago
  },
  {
    notificationId: 3,
    recipientEmployeeNumber: 1,
    requestId: 98,
    eventType: 'CancellationApproved',
    title: 'Cancellation Finalized',
    message: 'Cancellation for order #98 was approved and stock was restored to inventory.',
    isRead: true,
    createdAtUtc: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(), // 1 day ago
  },
]

export function getMockNotifications() {
  return [...mockStore]
}

export function getMockUnreadCount() {
  return mockStore.filter((n) => !n.isRead).length
}

export function markMockNotificationRead(id) {
  mockStore = mockStore.map((n) =>
    n.notificationId === id ? { ...n, isRead: true } : n
  )
}

export function markAllMockNotificationsRead() {
  mockStore = mockStore.map((n) => ({ ...n, isRead: true }))
}
