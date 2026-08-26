/** Shared surface container. */
export default function Card({ className = '', ...props }) {
  return (
    <div
      className={`rounded-card border border-surface-border bg-surface-card ${className}`}
      {...props}
    />
  )
}
