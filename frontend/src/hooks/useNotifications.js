import { useCallback, useEffect, useRef, useState } from 'react'
import {
  getNotifications,
  getUnreadCount,
  markAllNotificationsRead,
  markNotificationRead,
} from '../api/notifications.js'

const POLL_INTERVAL_MS = 30_000

/**
 * Notification bell state (Plan §4.2/T4.8): polls the unread count every 30s, pausing while
 * the tab is hidden (Plan §11 risk: "Notification polling hammers the DB" — mitigation is
 * exactly this pause, plus the count endpoint being a single indexed query server-side).
 *
 * The full feed is only fetched on demand (when the dropdown opens), not on every poll tick —
 * polling only needs the cheap count.
 *
 * `enabled` should be `!!user` from AuthContext — there is nothing to poll for while logged out.
 */
export function useNotifications(enabled) {
  const [unreadCount, setUnreadCount] = useState(0)
  const [items, setItems] = useState([])
  const [feedLoading, setFeedLoading] = useState(false)
  const [feedError, setFeedError] = useState(null)
  const [feedLoaded, setFeedLoaded] = useState(false)
  const enabledRef = useRef(enabled)
  enabledRef.current = enabled

  const refreshUnreadCount = useCallback(async () => {
    if (!enabledRef.current) return
    try {
      const { count } = await getUnreadCount()
      setUnreadCount(count)
    } catch {
      // A single missed poll tick isn't worth surfacing as an error state — it'll retry
      // in 30s. Only the on-demand feed fetch surfaces errors to the user.
    }
  }, [])

  useEffect(() => {
    if (!enabled) {
      setUnreadCount(0)
      return undefined
    }

    refreshUnreadCount()

    function tick() {
      if (document.hidden) return
      refreshUnreadCount()
    }

    const intervalId = setInterval(tick, POLL_INTERVAL_MS)

    // Also refresh immediately when the tab regains visibility, instead of waiting up to
    // 30s for the next tick, so the badge doesn't look stale right after switching back.
    function onVisibilityChange() {
      if (!document.hidden) refreshUnreadCount()
    }
    document.addEventListener('visibilitychange', onVisibilityChange)

    return () => {
      clearInterval(intervalId)
      document.removeEventListener('visibilitychange', onVisibilityChange)
    }
  }, [enabled, refreshUnreadCount])

  const loadFeed = useCallback(async () => {
    setFeedLoading(true)
    setFeedError(null)
    try {
      const result = await getNotifications({ pageSize: 20 })
      setItems(result.items)
      setFeedLoaded(true)
    } catch {
      setFeedError('Could not load notifications.')
    } finally {
      setFeedLoading(false)
    }
  }, [])

  const markRead = useCallback(async (id) => {
    setItems((current) => current.map((n) => (n.id === id ? { ...n, isRead: true } : n)))
    setUnreadCount((current) => Math.max(0, current - 1))
    try {
      await markNotificationRead(id)
    } catch {
      // Best-effort: refresh from the server to correct any optimistic-update drift.
      await refreshUnreadCount()
      await loadFeed()
    }
  }, [loadFeed, refreshUnreadCount])

  const markAllRead = useCallback(async () => {
    setItems((current) => current.map((n) => ({ ...n, isRead: true })))
    setUnreadCount(0)
    try {
      await markAllNotificationsRead()
    } catch {
      await refreshUnreadCount()
      await loadFeed()
    }
  }, [loadFeed, refreshUnreadCount])

  return {
    unreadCount,
    items,
    feedLoading,
    feedError,
    feedLoaded,
    loadFeed,
    markRead,
    markAllRead,
  }
}
