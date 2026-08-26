import { Search, Bell, User, Menu } from 'lucide-react'

/**
 * Top bar. SHARED COMPONENT.
 *
 * The global search box and the notification bell are rendered because they appear on every
 * approved wireframe, but neither is wired:
 *  - global search across inventory/requests/suppliers has no endpoint in the Plan's catalogue (§4.2);
 *  - the unread badge belongs to the notifications feature (Plan M4, `GET /api/v1/notifications/unread-count`,
 *    polled every 30s) and is owned by M4.
 * Both are left inert rather than faked. Do not add a mock count here.
 */
export default function Header({ onMenuClick }) {
  return (
    <header className="sticky top-0 z-20 flex h-16 items-center gap-3 border-b border-surface-border bg-surface-card px-4 sm:px-6">
      <button
        type="button"
        onClick={onMenuClick}
        className="-ml-1 rounded-md p-2 text-ink-muted hover:bg-surface-muted lg:hidden"
        aria-label="Open navigation"
      >
        <Menu className="h-5 w-5" />
      </button>

      <div className="relative min-w-0 flex-1 sm:max-w-xl">
        <Search
          className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-ink-subtle"
          aria-hidden="true"
        />
        <input
          type="search"
          disabled
          placeholder="Search inventory, requests, or suppliers..."
          aria-label="Global search (not yet available)"
          title="Global search is not implemented yet"
          className="w-full rounded-md border border-transparent bg-surface-muted py-2 pl-9 pr-3 text-sm text-ink placeholder:text-ink-subtle disabled:cursor-not-allowed"
        />
      </div>

      <div className="ml-auto flex items-center gap-2">
        <button
          type="button"
          disabled
          title="Notifications are not implemented yet"
          aria-label="Notifications (not yet available)"
          className="rounded-md p-2 text-ink-muted disabled:cursor-not-allowed"
        >
          <Bell className="h-5 w-5" />
        </button>
        <button
          type="button"
          disabled
          title="Account menu is not implemented yet"
          aria-label="Account (not yet available)"
          className="flex h-9 w-9 items-center justify-center rounded-full bg-brand-700 text-white disabled:cursor-not-allowed"
        >
          <User className="h-5 w-5" />
        </button>
      </div>
    </header>
  )
}
