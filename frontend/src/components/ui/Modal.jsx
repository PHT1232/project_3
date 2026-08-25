import { useEffect } from 'react'
import { X } from 'lucide-react'

/**
 * Shared modal dialog. Closes on Escape and on scrim click.
 * Kept deliberately small — no focus-trap library, no portal, no animation.
 */
export default function Modal({ open, onClose, title, children, footer }) {
  useEffect(() => {
    if (!open) return undefined
    function onKeyDown(e) {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [open, onClose])

  if (!open) return null

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-ink/40" onClick={onClose} aria-hidden="true" />
      <div
        role="dialog"
        aria-modal="true"
        aria-label={title}
        className="relative z-10 w-full max-w-md rounded-card border border-surface-border bg-surface-card shadow-xl"
      >
        <div className="flex items-center justify-between border-b border-surface-border px-5 py-4">
          <h2 className="text-base font-bold tracking-tight text-ink">{title}</h2>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close dialog"
            className="rounded p-1 text-ink-muted hover:bg-surface-muted hover:text-ink"
          >
            <X className="h-4 w-4" />
          </button>
        </div>
        <div className="px-5 py-4">{children}</div>
        {footer && (
          <div className="flex justify-end gap-2 border-t border-surface-border px-5 py-4">
            {footer}
          </div>
        )}
      </div>
    </div>
  )
}
