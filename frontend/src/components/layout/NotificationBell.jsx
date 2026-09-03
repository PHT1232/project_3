import { useEffect, useRef, useState } from 'react'
import { Bell } from 'lucide-react'
import { useAuth } from '../../contexts/AuthContext.jsx'
import { useNotifications } from '../../hooks/useNotifications.js'
import { SkeletonList } from '../ui/Skeleton.jsx'

function relativeTime(isoString) {
  const seconds = Math.max(0, Math.floor((Date.now() - new Date(isoString).getTime()) / 1000))
  if (seconds < 60) return 'just now'
  const minutes = Math.floor(seconds / 60)
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ago`
  const days = Math.floor(hours / 24)
  return `${days}d ago`
}

/**
 * Bell icon + unread badge + dropdown feed (Plan §4.2/T4.8). Replaces the disabled placeholder
 * that used to live directly in Header.jsx.
 */
export default function NotificationBell() {
  const { user } = useAuth()
  const { unreadCount, items, feedLoading, feedError, loadFeed, markRead, markAllRead } =
    useNotifications(!!user)
  const [open, setOpen] = useState(false)
  const containerRef = useRef(null)

  function toggleOpen() {
    setOpen((wasOpen) => {
      const willOpen = !wasOpen
      if (willOpen) loadFeed()
      return willOpen
    })
  }

  // Close on outside click — same pattern as the account menu elsewhere in Header.jsx.
  useEffect(() => {
    if (!open) return undefined
    function onPointerDown(event) {
      if (containerRef.current && !containerRef.current.contains(event.target)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', onPointerDown)
    return () => document.removeEventListener('mousedown', onPointerDown)
  }, [open])

  return (
    <div className="relative" ref={containerRef}>
      <button
        type="button"
        onClick={toggleOpen}
        aria-haspopup="true"
        aria-expanded={open}
        aria-label={unreadCount > 0 ? `Notifications (${unreadCount} unread)` : 'Notifications'}
        className="relative rounded-md p-2 text-ink-muted hover:bg-surface-muted"
      >
        <Bell className="h-5 w-5" />
        {unreadCount > 0 && (
          <span
            className="absolute -right-0.5 -top-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-status-danger px-1 text-[10px] font-semibold leading-none text-white"
            aria-hidden="true"
          >
            {unreadCount > 99 ? '99+' : unreadCount}
          </span>
        )}
      </button>

      {open && (
        <div className="absolute right-0 z-20 mt-2 w-80 rounded-md border border-surface-border bg-surface-card shadow-lg">
          <div className="flex items-center justify-between border-b border-surface-border px-3 py-2">
            <p className="text-sm font-semibold text-ink">Notifications</p>
            {unreadCount > 0 && (
              <button
                type="button"
                onClick={markAllRead}
                className="text-xs font-medium text-brand-700 hover:underline"
              >
                Mark all read
              </button>
            )}
          </div>

          <div className="max-h-96 overflow-y-auto">
            {feedLoading && (
              <SkeletonList
                label="Loading notifications…"
                rows={3}
                lines={3}
                trailing={false}
                rowClassName="px-3 py-2.5"
              />
            )}

            {!feedLoading && feedError && (
              <p className="px-3 py-6 text-center text-sm text-status-danger">{feedError}</p>
            )}

            {!feedLoading && !feedError && items.length === 0 && (
              <p className="px-3 py-6 text-center text-sm text-ink-muted">
                You&apos;re all caught up.
              </p>
            )}

            {!feedLoading &&
              !feedError &&
              items.map((notification) => (
                <button
                  key={notification.id}
                  type="button"
                  onClick={() => !notification.isRead && markRead(notification.id)}
                  className={[
                    'block w-full border-b border-surface-border px-3 py-2.5 text-left last:border-b-0 hover:bg-surface-muted',
                    notification.isRead ? '' : 'bg-brand-50',
                  ].join(' ')}
                >
                  <div className="flex items-start justify-between gap-2">
                    <p className="text-sm font-medium text-ink">{notification.title}</p>
                    {!notification.isRead && (
                      <span
                        className="mt-1 h-1.5 w-1.5 shrink-0 rounded-full bg-brand-600"
                        aria-label="Unread"
                      />
                    )}
                  </div>
                  <p className="mt-0.5 text-xs text-ink-muted">{notification.message}</p>
                  <p className="mt-1 text-[11px] text-ink-subtle">
                    {relativeTime(notification.createdAtUtc)}
                  </p>
                </button>
              ))}
          </div>
        </div>
      )}
    </div>
  )
}
