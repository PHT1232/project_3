import { NavLink } from 'react-router-dom'
import { Files } from 'lucide-react'
import { navItems } from '../../navigation.js'

/**
 * Primary navigation rail. SHARED COMPONENT.
 *
 * Collapses to an off-canvas drawer under `lg`; the parent AppLayout owns the open state.
 */
export default function Sidebar({ open, onNavigate }) {
  return (
    <>
      {/* Scrim, mobile only */}
      {open && (
        <div
          className="fixed inset-0 z-30 bg-ink/40 lg:hidden"
          aria-hidden="true"
          onClick={onNavigate}
        />
      )}

      <aside
        className={[
          'fixed inset-y-0 left-0 z-40 flex w-64 shrink-0 flex-col border-r border-surface-border bg-surface-card',
          'transition-transform duration-200 lg:translate-x-0',
          open ? 'translate-x-0' : '-translate-x-full',
        ].join(' ')}
      >
        <div className="flex h-16 items-center gap-2 px-6">
          <Files className="h-6 w-6 text-brand-700" aria-hidden="true" />
          <span className="text-lg font-bold tracking-tight text-brand-700">StationeryMS</span>
        </div>

        <nav aria-label="Main" className="flex-1 overflow-y-auto px-3 pb-6">
          <ul className="space-y-1">
            {navItems.map(({ to, label, icon: Icon, end }) => (
              <li key={to}>
                <NavLink
                  to={to}
                  end={end}
                  onClick={onNavigate}
                  className={({ isActive }) =>
                    [
                      'flex items-center gap-3 rounded-md px-3 py-2.5 text-sm transition-colors',
                      isActive
                        ? 'bg-brand-50 font-semibold text-brand-700'
                        : 'text-ink-muted hover:bg-surface-muted hover:text-ink',
                    ].join(' ')
                  }
                >
                  <Icon className="h-5 w-5 shrink-0" aria-hidden="true" />
                  <span>{label}</span>
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>
      </aside>
    </>
  )
}
