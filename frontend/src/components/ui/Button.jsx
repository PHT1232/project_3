const VARIANTS = {
  primary: 'bg-brand-700 text-white hover:bg-brand-800 disabled:bg-brand-700/50',
  secondary:
    'bg-surface-card text-ink border border-surface-border hover:bg-surface-muted disabled:text-ink-subtle',
  ghost: 'text-ink-muted hover:bg-surface-muted disabled:text-ink-subtle',
  muted: 'bg-surface-muted text-ink-muted border border-surface-border disabled:text-ink-subtle',
}

const SIZES = {
  sm: 'h-8 px-3 text-xs gap-1.5',
  md: 'h-10 px-4 text-sm gap-2',
}

/** Shared button. `as` allows rendering a Link while keeping the same visual treatment. */
export default function Button({
  variant = 'primary',
  size = 'md',
  className = '',
  as: Tag = 'button',
  type,
  ...props
}) {
  return (
    <Tag
      type={Tag === 'button' ? (type ?? 'button') : type}
      className={[
        'inline-flex items-center justify-center rounded-md font-semibold transition-colors',
        'disabled:cursor-not-allowed',
        VARIANTS[variant],
        SIZES[size],
        className,
      ].join(' ')}
      {...props}
    />
  )
}
