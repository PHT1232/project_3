import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Search, User, Menu, LogOut } from 'lucide-react'
import { useAuth } from '../../contexts/AuthContext.jsx'
import NotificationBell from './NotificationBell.jsx'

/**
 * Top bar. SHARED COMPONENT.
 *
 * The global search box is rendered because it appears on every approved wireframe, but isn't
 * wired — global search across inventory/requests/suppliers has no endpoint in the Plan's
 * catalogue (§4.2). Left inert rather than faked.
 *
 * The notification bell (Plan M4, `GET /api/v1/notifications/unread-count`, polled every 30s)
 * is wired — see NotificationBell.jsx.
 *
 * The account menu is wired: it shows the signed-in user and logs out (Plan §5 — local
 * session destruction, then navigate to /login with history replacement).
 */
export default function Header({ onMenuClick }) {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [menuOpen, setMenuOpen] = useState(false)

  function handleLogout() {
    setMenuOpen(false)
    logout()
    navigate('/login', { replace: true })
  }

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
        <NotificationBell />

        <div className="relative">
          <button
            type="button"
            onClick={() => setMenuOpen((open) => !open)}
            aria-haspopup="true"
            aria-expanded={menuOpen}
            aria-label="Account menu"
            className="flex h-9 w-9 items-center justify-center rounded-full bg-brand-700 text-white"
          >
            <User className="h-5 w-5" />
          </button>

          {menuOpen && (
            <>
              <button
                type="button"
                className="fixed inset-0 z-10 cursor-default"
                aria-hidden="true"
                tabIndex={-1}
                onClick={() => setMenuOpen(false)}
              />
              <div className="absolute right-0 z-20 mt-2 w-56 rounded-md border border-surface-border bg-surface-card py-1 shadow-lg">
                <div className="border-b border-surface-border px-3 py-2">
                  <p className="truncate text-sm font-semibold text-ink">{user?.name}</p>
                  <p className="truncate text-xs text-ink-muted">{user?.role}</p>
                </div>
                <button
                  type="button"
                  onClick={handleLogout}
                  className="flex w-full items-center gap-2 px-3 py-2 text-sm text-ink hover:bg-surface-muted"
                >
                  <LogOut className="h-4 w-4" aria-hidden="true" />
                  Log out
                </button>
              </div>
            </>
          )}
        </div>
      </div>
    </header>
  )
}
