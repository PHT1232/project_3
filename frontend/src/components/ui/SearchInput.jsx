import { Search } from 'lucide-react'

/** Shared search field with a leading icon. Controlled. */
export default function SearchInput({ value, onChange, placeholder, label, className = '' }) {
  return (
    <div className={`relative ${className}`}>
      <Search
        className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-ink-subtle"
        aria-hidden="true"
      />
      <input
        type="search"
        value={value}
        aria-label={label ?? placeholder}
        placeholder={placeholder}
        onChange={(e) => onChange(e.target.value)}
        className="h-10 w-full rounded-md border border-surface-border bg-surface-card pl-9 pr-3 text-sm text-ink placeholder:text-ink-subtle"
      />
    </div>
  )
}
