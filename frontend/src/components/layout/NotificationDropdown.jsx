import { useNavigate } from 'react-router-dom'
import { CheckCheck, ExternalLink, Loader2, Inbox } from 'lucide-react'
import { formatRelativeTime } from '../../lib/format.js'

/**
 * Dropdown panel showing recent notifications.
 */
export default function NotificationDropdown({
  open,
  onClose,
  notifications,
  unreadCount,
  loading,
  error,
  onMarkRead,
  onMarkAllRead,
}) {
  const navigate = useNavigate()

  if (!open) return null

  function handleItemClick(item) {
    if (!item.isRead) {
      onMarkRead(item.notificationId ?? item.id)
    }
    if (item.requestId) {
      onClose()
      navigate('/my-requests')
    }
  }

  return (
    <>
      {/* Click-outside backdrop */}
      <button
        type="button"
        className="fixed inset-0 z-20 cursor-default"
        aria-hidden="true"
        tabIndex={-1}
        onClick={onClose}
      />

      <div
        role="dialog"
        aria-label="Notifications"
        className="absolute right-0 z-30 mt-2 w-80 sm:w-96 rounded-lg border border-surface-border bg-surface-card shadow-xl overflow-hidden"
      >
        {/* Header */}
        <div className="flex items-center justify-between border-b border-surface-border px-4 py-3 bg-surface-card">
          <div className="flex items-center gap-2">
            <h3 className="text-sm font-semibold text-ink">Notifications</h3>
            {unreadCount > 0 && (
              <span className="rounded-full bg-brand-100 px-2 py-0.5 text-xs font-semibold text-brand-700">
                {unreadCount} unread
              </span>
            )}
          </div>
          {unreadCount > 0 && (
            <button
              type="button"
              onClick={onMarkAllRead}
              className="inline-flex items-center gap-1 text-xs font-medium text-brand-600 hover:text-brand-700 transition-colors"
            >
              <CheckCheck className="h-3.5 w-3.5" />
              Mark all read
            </button>
          )}
        </div>

        {/* Content List */}
        <div className="max-h-96 overflow-y-auto divide-y divide-surface-border">
          {loading ? (
            <div className="flex flex-col items-center justify-center py-8 text-ink-muted">
              <Loader2 className="h-6 w-6 animate-spin mb-2 text-brand-600" />
              <p className="text-xs">Loading notifications...</p>
            </div>
          ) : error ? (
            <div className="px-4 py-6 text-center text-xs text-status-danger">
              <p>{error}</p>
            </div>
          ) : notifications.length === 0 ? (
            <div className="flex flex-col items-center justify-center px-4 py-8 text-center text-ink-muted">
              <div className="mb-2 rounded-full bg-surface-muted p-3">
                <Inbox className="h-6 w-6 text-ink-subtle" />
              </div>
              <p className="text-sm font-medium text-ink">No notifications yet</p>
              <p className="mt-1 text-xs text-ink-muted">
                You will be notified when your requests or approvals change.
              </p>
            </div>
          ) : (
            notifications.map((item) => {
              const id = item.notificationId ?? item.id
              const isUnread = !item.isRead

              return (
                <div
                  key={id}
                  onClick={() => handleItemClick(item)}
                  role="button"
                  tabIndex={0}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault()
                      handleItemClick(item)
                    }
                  }}
                  className={`group relative flex cursor-pointer gap-3 px-4 py-3 text-left transition-colors hover:bg-surface-muted ${
                    isUnread ? 'bg-brand-50/40' : 'bg-surface-card'
                  }`}
                >
                  {/* Unread indicator dot */}
                  <div className="pt-1">
                    <span
                      className={`inline-block h-2 w-2 rounded-full ${
                        isUnread ? 'bg-brand-600' : 'bg-transparent'
                      }`}
                      aria-hidden="true"
                    />
                  </div>

                  {/* Notification Content */}
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center justify-between gap-1">
                      <p
                        className={`truncate text-xs font-semibold ${
                          isUnread ? 'text-ink' : 'text-ink-muted'
                        }`}
                      >
                        {item.title}
                      </p>
                      <span className="shrink-0 text-[11px] text-ink-subtle">
                        {formatRelativeTime(item.createdAtUtc)}
                      </span>
                    </div>

                    <p className="mt-0.5 text-xs text-ink-muted line-clamp-2">
                      {item.message}
                    </p>

                    {item.requestId && (
                      <div className="mt-1 flex items-center gap-1 text-[11px] font-medium text-brand-600 group-hover:text-brand-700">
                        <span>View request #{item.requestId}</span>
                        <ExternalLink className="h-3 w-3" />
                      </div>
                    )}
                  </div>
                </div>
              )
            })
          )}
        </div>
      </div>
    </>
  )
}
