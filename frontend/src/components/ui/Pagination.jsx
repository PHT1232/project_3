import Button from './Button.jsx'

/**
 * Shared table pagination footer. The markup and wording follow the Catalogue page, which
 * set the pattern: "Page 1 of 4 · 40 items" on the left, Previous/Next on the right.
 *
 * Carries `data-print-hide` so the control never appears on a printed report.
 */
export default function Pagination({
  page,
  totalPages,
  total,
  onPageChange,
  noun = 'item',
  nounPlural,
  className = '',
}) {
  const plural = nounPlural ?? `${noun}s`

  return (
    <div
      data-print-hide
      className={`flex items-center justify-between border-t border-surface-border px-4 py-3 text-sm text-ink-muted ${className}`}
    >
      <span>
        Page {page} of {totalPages} · {total} {total === 1 ? noun : plural}
      </span>
      <div className="flex gap-2">
        <Button
          variant="secondary"
          size="sm"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
        >
          Previous
        </Button>
        <Button
          variant="secondary"
          size="sm"
          disabled={page >= totalPages}
          onClick={() => onPageChange(page + 1)}
        >
          Next
        </Button>
      </div>
    </div>
  )
}
