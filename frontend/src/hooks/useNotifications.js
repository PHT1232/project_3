import { useState, useEffect, useCallback, useRef } from 'react'
import {
  fetchUnreadCount,
  fetchNotifications,
  markAsRead,
  markAllAsRead,
} from '../api/notifications.js'
import { useAuth } from '../contexts/AuthContext.jsx'

const POLL_INTERVAL_MS = 30000 // 30 seconds per Plan §3.4 / §4.2

/**
 * Hook to manage notification polling, unread count badge, and notification list state.
 */
export function useNotifications() {
  const { isAuthenticated } = useAuth()
  const [unreadCount, setUnreadCount] = useState(0)
  const [notifications, setNotifications] = useState([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const isPollingRef = useRef(false)

  // Fetch unread count quietly (used for initial load and polling)
  const pollUnreadCount = useCallback(async () => {
    if (!isAuthenticated || isPollingRef.current) return
    try {
      isPollingRef.current = true
      const count = await fetchUnreadCount()
      setUnreadCount(count)
    } catch {
      // Polling handles errors silently without crashing UI
    } finally {
      isPollingRef.current = false
    }
  }, [isAuthenticated])

  // Periodic polling every 30 seconds while user is authenticated
  useEffect(() => {
    if (!isAuthenticated) {
      setUnreadCount(0)
      setNotifications([])
      return undefined
    }

    pollUnreadCount()

    const intervalId = setInterval(pollUnreadCount, POLL_INTERVAL_MS)
    return () => clearInterval(intervalId)
  }, [isAuthenticated, pollUnreadCount])

  // Fetch the full feed when the notification popover is opened
  const loadNotifications = useCallback(async () => {
    if (!isAuthenticated) return
    setLoading(true)
    setError(null)
    try {
      const data = await fetchNotifications({ page: 1, pageSize: 10 })
      const items = Array.isArray(data) ? data : data?.items ?? []
      setNotifications(items)
      const count = await fetchUnreadCount()
      setUnreadCount(count)
    } catch (err) {
      setError(err?.response?.data?.message || 'Failed to load notifications.')
    } finally {
      setLoading(false)
    }
  }, [isAuthenticated])

  // Mark a single notification as read
  const markOneRead = useCallback(
    async (notificationId) => {
      try {
        // Optimistic UI update
        setNotifications((prev) =>
          prev.map((n) =>
            (n.notificationId === notificationId || n.id === notificationId)
              ? { ...n, isRead: true }
              : n
          )
        )
        setUnreadCount((prev) => Math.max(0, prev - 1))
        await markAsRead(notificationId)
      } catch {
        pollUnreadCount()
      }
    },
    [pollUnreadCount]
  )

  // Mark all notifications as read
  const markAllRead = useCallback(async () => {
    try {
      // Optimistic UI update
      setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })))
      setUnreadCount(0)
      await markAllAsRead()
    } catch {
      pollUnreadCount()
    }
  }, [pollUnreadCount])

  return {
    unreadCount,
    notifications,
    loading,
    error,
    loadNotifications,
    markOneRead,
    markAllRead,
    refreshUnreadCount: pollUnreadCount,
  }
}
